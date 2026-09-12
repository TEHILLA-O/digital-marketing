using LedgerX.Contracts.Api;
using LedgerX.SharedKernel.Paging;

namespace LedgerX.Payments.Application;

public interface ITransferService
{
    Task<TransferResponse> TransferAsync(Guid customerId, CreateTransferRequest request, string idempotencyKey, CancellationToken cancellationToken);

    Task<TransferResponse> DepositAsync(Guid customerId, CreateDepositRequest request, string idempotencyKey, bool isStaff, CancellationToken cancellationToken);

    Task<TransferResponse> WithdrawAsync(Guid customerId, CreateWithdrawalRequest request, string idempotencyKey, CancellationToken cancellationToken);

    Task<TransferResponse> GetAsync(Guid transferId, Guid? customerId, bool isStaff, CancellationToken cancellationToken);

    Task<PagedResult<TransferResponse>> ListAsync(Guid? customerId, string? status, PageRequest page, CancellationToken cancellationToken);
}

public interface IAccountsGateway
{
    Task<AccountSnapshotDto> GetAccountAsync(Guid accountId, CancellationToken cancellationToken);

    Task UpdateProjectedBalanceAsync(Guid accountId, decimal balance, CancellationToken cancellationToken);
}

public interface ILedgerGateway
{
    Task EnsureCustomerAccountAsync(Guid bankAccountId, Guid customerId, string currency, string displayName, CancellationToken cancellationToken);

    Task<JournalResponse> PostTransferAsync(Guid source, Guid destination, decimal amount, string currency, string reference, string idempotencyKey, Guid sourceId, CancellationToken cancellationToken);

    Task<JournalResponse> PostDepositAsync(Guid accountId, decimal amount, string currency, string reference, string idempotencyKey, Guid sourceId, CancellationToken cancellationToken);

    Task<JournalResponse> PostWithdrawalAsync(Guid accountId, decimal amount, string currency, string reference, string idempotencyKey, Guid sourceId, CancellationToken cancellationToken);

    Task<decimal> GetBalanceAsync(Guid accountId, CancellationToken cancellationToken);
}

public sealed record AccountSnapshotDto(
    Guid AccountId,
    Guid CustomerId,
    string AccountNumber,
    string Currency,
    string Status,
    decimal ProjectedBalance,
    bool CanSend,
    bool CanReceive);
