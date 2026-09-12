using LedgerX.Accounts.Application;
using LedgerX.Accounts.Domain;
using LedgerX.Contracts.Api;
using LedgerX.Contracts.Events;
using LedgerX.Eventing.Outbox;
using LedgerX.SharedKernel;
using LedgerX.SharedKernel.Context;
using LedgerX.SharedKernel.Money;
using LedgerX.SharedKernel.Paging;
using LedgerX.SharedKernel.Primitives;
using LedgerX.SharedKernel.Time;

using Microsoft.EntityFrameworkCore;

namespace LedgerX.Accounts.Infrastructure;

public sealed class AccountService(
    AccountsDbContext db,
    IOutboxWriter outbox,
    IClock clock,
    ICorrelationAccessor correlation) : IAccountService
{
    public async Task<AccountResponse> OpenAsync(Guid customerId, OpenAccountRequest request, CancellationToken cancellationToken)
    {
        if (!Enum.TryParse<AccountType>(request.AccountType, true, out var type))
        {
            throw new DomainException("Account type must be Current or Savings.");
        }

        var currency = Currency.Parse(string.IsNullOrWhiteSpace(request.Currency) ? Currency.Default.Code : request.Currency);
        var account = BankAccount.Open(CustomerId.From(customerId), type, currency, request.DisplayName, clock);
        db.Accounts.Add(account);

        var evt = IntegrationEvents.Create(
            EventTypes.AccountCreated,
            new AccountCreatedV1(account.Id.Value, customerId, account.AccountNumber.Value, account.AccountNumber.SortCode, currency.Code, type.ToString()),
            correlation.Current.CorrelationId);
        await outbox.EnqueueAsync(KafkaTopics.Accounts, evt, cancellationToken).ConfigureAwait(false);
        await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return Map(account);
    }

    public async Task<IReadOnlyList<AccountResponse>> ListForCustomerAsync(Guid customerId, CancellationToken cancellationToken)
    {
        var items = await db.Accounts.AsNoTracking()
            .Where(a => a.CustomerId == CustomerId.From(customerId))
            .OrderBy(a => a.CreatedAt)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
        return items.Select(Map).ToList();
    }

    public async Task<AccountResponse> GetAsync(Guid accountId, Guid? actingCustomerId, bool isStaff, CancellationToken cancellationToken)
    {
        var account = await db.Accounts.AsNoTracking()
            .FirstOrDefaultAsync(a => a.Id == BankAccountId.From(accountId), cancellationToken)
            .ConfigureAwait(false) ?? throw new NotFoundException("BankAccount", accountId);

        if (!isStaff && actingCustomerId != account.CustomerId.Value)
        {
            throw new ForbiddenException();
        }

        return Map(account);
    }

    public async Task<AccountResponse> FreezeAsync(Guid accountId, Guid actorId, string reason, CancellationToken cancellationToken)
    {
        var account = await db.Accounts.FirstOrDefaultAsync(a => a.Id == BankAccountId.From(accountId), cancellationToken)
            .ConfigureAwait(false) ?? throw new NotFoundException("BankAccount", accountId);

        account.Freeze(clock);
        var evt = IntegrationEvents.Create(
            EventTypes.AccountFrozen,
            new AccountFrozenV1(account.Id.Value, account.CustomerId.Value, actorId, reason),
            correlation.Current.CorrelationId);
        await outbox.EnqueueAsync(KafkaTopics.Accounts, evt, cancellationToken).ConfigureAwait(false);

        var security = IntegrationEvents.Create(
            EventTypes.SecurityEventRaised,
            new SecurityEventRaisedV1("ACCOUNT_FROZEN", actorId.ToString(), "BankAccount", accountId.ToString(), reason),
            correlation.Current.CorrelationId);
        await outbox.EnqueueAsync(KafkaTopics.Security, security, cancellationToken).ConfigureAwait(false);

        await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return Map(account);
    }

    public async Task<AccountResponse> UnfreezeAsync(Guid accountId, Guid actorId, CancellationToken cancellationToken)
    {
        var account = await db.Accounts.FirstOrDefaultAsync(a => a.Id == BankAccountId.From(accountId), cancellationToken)
            .ConfigureAwait(false) ?? throw new NotFoundException("BankAccount", accountId);

        account.Unfreeze(clock);
        var evt = IntegrationEvents.Create(
            EventTypes.AccountUnfrozen,
            new AccountUnfrozenV1(account.Id.Value, account.CustomerId.Value, actorId),
            correlation.Current.CorrelationId);
        await outbox.EnqueueAsync(KafkaTopics.Accounts, evt, cancellationToken).ConfigureAwait(false);

        var security = IntegrationEvents.Create(
            EventTypes.SecurityEventRaised,
            new SecurityEventRaisedV1("ACCOUNT_UNFROZEN", actorId.ToString(), "BankAccount", accountId.ToString(), null),
            correlation.Current.CorrelationId);
        await outbox.EnqueueAsync(KafkaTopics.Security, security, cancellationToken).ConfigureAwait(false);

        await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return Map(account);
    }

    public async Task<PagedResult<AccountResponse>> AdminListAsync(string? query, PageRequest page, CancellationToken cancellationToken)
    {
        var q = db.Accounts.AsNoTracking();
        if (!string.IsNullOrWhiteSpace(query))
        {
            var term = query.Trim();
            q = q.Where(a => a.DisplayName.Contains(term) || a.AccountNumber.Value.Contains(term));
        }

        var total = await q.CountAsync(cancellationToken).ConfigureAwait(false);
        var items = await q.OrderByDescending(a => a.CreatedAt).Skip(page.Skip).Take(page.Take).ToListAsync(cancellationToken).ConfigureAwait(false);
        return new PagedResult<AccountResponse>(items.Select(Map).ToList(), total, page.Page, page.Take);
    }

    public async Task<BeneficiaryResponse> AddBeneficiaryAsync(Guid ownerCustomerId, CreateBeneficiaryRequest request, CancellationToken cancellationToken)
    {
        var destination = await db.Accounts.FirstOrDefaultAsync(a => a.Id == BankAccountId.From(request.DestinationAccountId), cancellationToken)
            .ConfigureAwait(false) ?? throw new NotFoundException("BankAccount", request.DestinationAccountId);

        var beneficiary = Beneficiary.CreateInternal(CustomerId.From(ownerCustomerId), request.DisplayName, destination, clock);
        db.Beneficiaries.Add(beneficiary);
        await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return new BeneficiaryResponse(beneficiary.Id.Value, beneficiary.DisplayName, beneficiary.AccountNumber, beneficiary.SortCode, beneficiary.Currency.Code, beneficiary.LinkedBankAccountId?.Value);
    }

    public async Task<IReadOnlyList<BeneficiaryResponse>> ListBeneficiariesAsync(Guid ownerCustomerId, CancellationToken cancellationToken)
    {
        var items = await db.Beneficiaries.AsNoTracking()
            .Where(b => b.OwnerCustomerId == CustomerId.From(ownerCustomerId))
            .OrderBy(b => b.DisplayName)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return items.Select(b => new BeneficiaryResponse(b.Id.Value, b.DisplayName, b.AccountNumber, b.SortCode, b.Currency.Code, b.LinkedBankAccountId?.Value)).ToList();
    }

    public async Task ApplyProjectedBalanceAsync(Guid accountId, decimal balance, CancellationToken cancellationToken)
    {
        var account = await db.Accounts.FirstOrDefaultAsync(a => a.Id == BankAccountId.From(accountId), cancellationToken)
            .ConfigureAwait(false);
        if (account is null)
        {
            return;
        }

        account.ApplyProjectedBalance(balance, clock);
        await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task<AccountSnapshot> RequireSnapshotAsync(Guid accountId, CancellationToken cancellationToken)
    {
        var account = await db.Accounts.AsNoTracking()
            .FirstOrDefaultAsync(a => a.Id == BankAccountId.From(accountId), cancellationToken)
            .ConfigureAwait(false) ?? throw new NotFoundException("BankAccount", accountId);

        return new AccountSnapshot(
            account.Id.Value,
            account.CustomerId.Value,
            account.AccountNumber.Value,
            account.AccountNumber.SortCode,
            account.Currency.Code,
            account.Status.ToString(),
            account.AccountType.ToString(),
            account.ProjectedBalance,
            account.CanSendMoney,
            account.CanReceiveMoney);
    }

    private static AccountResponse Map(BankAccount account) =>
        new(
            account.Id.Value,
            account.CustomerId.Value,
            account.AccountNumber.Value,
            account.AccountNumber.SortCode,
            account.Currency.Code,
            account.AccountType.ToString(),
            account.Status.ToString(),
            account.DisplayName,
            account.ProjectedBalance,
            account.CreatedAt);
}
