using System.Diagnostics.Metrics;

using LedgerX.Contracts.Api;
using LedgerX.Contracts.Events;
using LedgerX.Eventing.Outbox;
using LedgerX.Payments.Application;
using LedgerX.Payments.Domain;
using LedgerX.SharedKernel;
using LedgerX.SharedKernel.Context;
using LedgerX.SharedKernel.Money;
using LedgerX.SharedKernel.Paging;
using LedgerX.SharedKernel.Primitives;
using LedgerX.SharedKernel.Time;

using Microsoft.EntityFrameworkCore;

using StackExchange.Redis;

namespace LedgerX.Payments.Infrastructure;

public sealed class TransferService(
    PaymentsDbContext db,
    IAccountsGateway accounts,
    ILedgerGateway ledger,
    IOutboxWriter outbox,
    RedisIdempotencyStore idempotency,
    IConnectionMultiplexer redis,
    IClock clock,
    ICorrelationAccessor correlation) : ITransferService
{
    private static readonly Meter Meter = new("LedgerX", "1.0.0");
    private static readonly Counter<long> TransfersTotal = Meter.CreateCounter<long>("ledgerx_transfers_total");
    private static readonly Counter<long> TransfersFailed = Meter.CreateCounter<long>("ledgerx_transfers_failed_total");
    private static readonly Histogram<double> TransferDuration = Meter.CreateHistogram<double>("ledgerx_transfer_duration");

    public Task<TransferResponse> TransferAsync(Guid customerId, CreateTransferRequest request, string idempotencyKey, CancellationToken cancellationToken) =>
        ExecuteAsync(
            customerId,
            idempotencyKey,
            request,
            async () =>
            {
                var source = await accounts.GetAccountAsync(request.SourceAccountId, cancellationToken).ConfigureAwait(false);
                var destination = await accounts.GetAccountAsync(request.DestinationAccountId, cancellationToken).ConfigureAwait(false);
                if (source.CustomerId != customerId)
                {
                    throw new ForbiddenException("You can only send from your own accounts.");
                }

                if (!source.CanSend)
                {
                    throw new DomainException("Frozen or inactive accounts cannot send transfers.", "account_not_active");
                }

                if (!destination.CanReceive)
                {
                    throw new DomainException("The destination account cannot receive funds.", "account_cannot_receive");
                }

                var currency = Currency.Parse(request.Currency ?? source.Currency);
                var amount = new Money(request.Amount, currency);
                if (source.Currency != currency.Code || destination.Currency != currency.Code)
                {
                    throw new DomainException("Cross-currency transfers are not supported in this simulation.");
                }

                var transfer = Transfer.Create(
                    PaymentKind.InternalTransfer,
                    CustomerId.From(customerId),
                    BankAccountId.From(source.AccountId),
                    BankAccountId.From(destination.AccountId),
                    null,
                    amount,
                    request.Description ?? "Internal transfer",
                    idempotencyKey,
                    clock);

                return await ProcessAsync(transfer, source, destination, cancellationToken).ConfigureAwait(false);
            },
            cancellationToken);

    public Task<TransferResponse> DepositAsync(Guid customerId, CreateDepositRequest request, string idempotencyKey, bool isStaff, CancellationToken cancellationToken) =>
        ExecuteAsync(
            customerId,
            idempotencyKey,
            request,
            async () =>
            {
                var account = await accounts.GetAccountAsync(request.AccountId, cancellationToken).ConfigureAwait(false);
                if (!isStaff && account.CustomerId != customerId)
                {
                    throw new ForbiddenException();
                }

                if (!account.CanReceive)
                {
                    throw new DomainException("This account cannot receive simulated deposits.");
                }

                var amount = new Money(request.Amount, Currency.Parse(request.Currency ?? account.Currency));
                var transfer = Transfer.Create(
                    PaymentKind.Deposit,
                    CustomerId.From(account.CustomerId),
                    BankAccountId.From(account.AccountId),
                    null,
                    null,
                    amount,
                    request.Description ?? "Simulated deposit",
                    idempotencyKey,
                    clock);

                return await ProcessAsync(transfer, account, null, cancellationToken).ConfigureAwait(false);
            },
            cancellationToken);

    public Task<TransferResponse> WithdrawAsync(Guid customerId, CreateWithdrawalRequest request, string idempotencyKey, CancellationToken cancellationToken) =>
        ExecuteAsync(
            customerId,
            idempotencyKey,
            request,
            async () =>
            {
                var account = await accounts.GetAccountAsync(request.AccountId, cancellationToken).ConfigureAwait(false);
                if (account.CustomerId != customerId)
                {
                    throw new ForbiddenException();
                }

                if (!account.CanSend)
                {
                    throw new DomainException("Frozen accounts cannot withdraw funds.", "account_not_active");
                }

                var amount = new Money(request.Amount, Currency.Parse(request.Currency ?? account.Currency));
                var transfer = Transfer.Create(
                    PaymentKind.Withdrawal,
                    CustomerId.From(customerId),
                    BankAccountId.From(account.AccountId),
                    null,
                    null,
                    amount,
                    request.Description ?? "Simulated withdrawal",
                    idempotencyKey,
                    clock);

                return await ProcessAsync(transfer, account, null, cancellationToken).ConfigureAwait(false);
            },
            cancellationToken);

    public async Task<TransferResponse> GetAsync(Guid transferId, Guid? customerId, bool isStaff, CancellationToken cancellationToken)
    {
        var transfer = await db.Transfers.AsNoTracking()
            .FirstOrDefaultAsync(t => t.Id == TransferId.From(transferId), cancellationToken)
            .ConfigureAwait(false) ?? throw new NotFoundException("Transfer", transferId);

        if (!isStaff && customerId != transfer.SenderCustomerId.Value)
        {
            throw new ForbiddenException();
        }

        return Map(transfer);
    }

    public async Task<PagedResult<TransferResponse>> ListAsync(Guid? customerId, string? status, PageRequest page, CancellationToken cancellationToken)
    {
        var q = db.Transfers.AsNoTracking().AsQueryable();
        if (customerId is { } cid)
        {
            q = q.Where(t => t.SenderCustomerId == CustomerId.From(cid));
        }

        if (!string.IsNullOrWhiteSpace(status) && Enum.TryParse<TransferStatus>(status, true, out var parsed))
        {
            q = q.Where(t => t.Status == parsed);
        }

        var total = await q.CountAsync(cancellationToken).ConfigureAwait(false);
        var items = await q.OrderByDescending(t => t.CreatedAt).Skip(page.Skip).Take(page.Take).ToListAsync(cancellationToken).ConfigureAwait(false);
        return new PagedResult<TransferResponse>(items.Select(Map).ToList(), total, page.Page, page.Take);
    }

    private async Task<TransferResponse> ExecuteAsync<TRequest>(
        Guid ownerId,
        string idempotencyKey,
        TRequest request,
        Func<Task<TransferResponse>> action,
        CancellationToken cancellationToken)
    {
        Guard.AgainstEmpty(idempotencyKey, "Idempotency-Key");
        var hash = RedisIdempotencyStore.HashRequest(request);
        var cached = await idempotency.TryGetAsync<TransferResponse>(ownerId.ToString(), idempotencyKey, hash, cancellationToken).ConfigureAwait(false);
        if (cached is not null)
        {
            return cached;
        }

        var started = clock.UtcNow;
        var result = await action().ConfigureAwait(false);
        await idempotency.SaveAsync(ownerId.ToString(), idempotencyKey, hash, result, cancellationToken).ConfigureAwait(false);
        TransferDuration.Record((clock.UtcNow - started).TotalMilliseconds);
        return result;
    }

    private async Task<TransferResponse> ProcessAsync(
        Transfer transfer,
        AccountSnapshotDto source,
        AccountSnapshotDto? destination,
        CancellationToken cancellationToken)
    {
        db.Transfers.Add(transfer);
        transfer.MarkValidated(clock);
        transfer.MarkProcessing(clock);
        await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        var created = IntegrationEvents.Create(
            EventTypes.TransferCreated,
            new TransferCreatedV1(
                transfer.Id.Value,
                transfer.Reference,
                transfer.Kind.ToString(),
                transfer.SenderCustomerId.Value,
                transfer.SourceAccountId.Value,
                transfer.DestinationAccountId?.Value,
                transfer.Amount.Amount,
                transfer.Amount.Currency.Code,
                transfer.IdempotencyKey),
            correlation.Current.CorrelationId);
        await outbox.EnqueueAsync(KafkaTopics.Payments, created, cancellationToken).ConfigureAwait(false);

        try
        {
            await ledger.EnsureCustomerAccountAsync(source.AccountId, source.CustomerId, source.Currency, source.AccountNumber, cancellationToken).ConfigureAwait(false);
            if (destination is not null)
            {
                await ledger.EnsureCustomerAccountAsync(destination.AccountId, destination.CustomerId, destination.Currency, destination.AccountNumber, cancellationToken).ConfigureAwait(false);
            }

            var journal = await RedisLock.ExecuteAsync(redis, $"account:{source.AccountId}", TimeSpan.FromSeconds(15), async () =>
                transfer.Kind switch
                {
                    PaymentKind.Deposit => await ledger.PostDepositAsync(source.AccountId, transfer.Amount.Amount, transfer.Amount.Currency.Code, transfer.Reference, transfer.IdempotencyKey, transfer.Id.Value, cancellationToken).ConfigureAwait(false),
                    PaymentKind.Withdrawal => await ledger.PostWithdrawalAsync(source.AccountId, transfer.Amount.Amount, transfer.Amount.Currency.Code, transfer.Reference, transfer.IdempotencyKey, transfer.Id.Value, cancellationToken).ConfigureAwait(false),
                    _ => await ledger.PostTransferAsync(source.AccountId, destination!.AccountId, transfer.Amount.Amount, transfer.Amount.Currency.Code, transfer.Reference, transfer.IdempotencyKey, transfer.Id.Value, cancellationToken).ConfigureAwait(false)
                }).ConfigureAwait(false);

            transfer.MarkCompleted(JournalId.From(journal.JournalId), clock);
            TransfersTotal.Add(1);

            if (transfer.Kind == PaymentKind.Deposit)
            {
                await outbox.EnqueueAsync(KafkaTopics.Payments, IntegrationEvents.Create(
                    EventTypes.FundsDeposited,
                    new FundsDepositedV1(transfer.Id.Value, source.AccountId, source.CustomerId, transfer.Amount.Amount, transfer.Amount.Currency.Code, journal.JournalId),
                    correlation.Current.CorrelationId), cancellationToken).ConfigureAwait(false);
            }
            else if (transfer.Kind == PaymentKind.Withdrawal)
            {
                await outbox.EnqueueAsync(KafkaTopics.Payments, IntegrationEvents.Create(
                    EventTypes.FundsWithdrawn,
                    new FundsWithdrawnV1(transfer.Id.Value, source.AccountId, source.CustomerId, transfer.Amount.Amount, transfer.Amount.Currency.Code, journal.JournalId),
                    correlation.Current.CorrelationId), cancellationToken).ConfigureAwait(false);
            }

            await outbox.EnqueueAsync(KafkaTopics.Payments, IntegrationEvents.Create(
                EventTypes.TransferCompleted,
                new TransferCompletedV1(transfer.Id.Value, transfer.Reference, journal.JournalId, source.AccountId, destination?.AccountId, transfer.Amount.Amount, transfer.Amount.Currency.Code),
                correlation.Current.CorrelationId), cancellationToken).ConfigureAwait(false);

            var sourceBalance = await ledger.GetBalanceAsync(source.AccountId, cancellationToken).ConfigureAwait(false);
            await accounts.UpdateProjectedBalanceAsync(source.AccountId, sourceBalance, cancellationToken).ConfigureAwait(false);
            if (destination is not null)
            {
                var destBalance = await ledger.GetBalanceAsync(destination.AccountId, cancellationToken).ConfigureAwait(false);
                await accounts.UpdateProjectedBalanceAsync(destination.AccountId, destBalance, cancellationToken).ConfigureAwait(false);
            }
        }
        catch (Exception ex) when (ex is DomainException or HttpRequestException or ConcurrencyConflictException)
        {
            TransfersFailed.Add(1);
            if (ex is InsufficientFundsException)
            {
                transfer.Reject(ex.Message, clock);
            }
            else
            {
                transfer.Fail(ex.Message, clock);
            }

            await outbox.EnqueueAsync(KafkaTopics.Payments, IntegrationEvents.Create(
                EventTypes.TransferFailed,
                new TransferFailedV1(transfer.Id.Value, transfer.Reference, transfer.Status.ToString(), ex.Message),
                correlation.Current.CorrelationId), cancellationToken).ConfigureAwait(false);
        }

        await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return Map(transfer);
    }

    private static TransferResponse Map(Transfer transfer) =>
        new(
            transfer.Id.Value,
            transfer.Reference,
            transfer.Kind.ToString(),
            transfer.SourceAccountId.Value,
            transfer.DestinationAccountId?.Value,
            transfer.Amount.Amount,
            transfer.Amount.Currency.Code,
            transfer.Description,
            transfer.Status.ToString(),
            transfer.FailureReason,
            transfer.JournalId?.Value,
            transfer.CreatedAt,
            transfer.CompletedAt);
}
