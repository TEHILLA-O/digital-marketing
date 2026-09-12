using LedgerX.Contracts.Api;
using LedgerX.SharedKernel.Paging;

namespace LedgerX.Accounts.Application;

public interface IAccountService
{
    Task<AccountResponse> OpenAsync(Guid customerId, OpenAccountRequest request, CancellationToken cancellationToken);

    Task<IReadOnlyList<AccountResponse>> ListForCustomerAsync(Guid customerId, CancellationToken cancellationToken);

    Task<AccountResponse> GetAsync(Guid accountId, Guid? actingCustomerId, bool isStaff, CancellationToken cancellationToken);

    Task<AccountResponse> FreezeAsync(Guid accountId, Guid actorId, string reason, CancellationToken cancellationToken);

    Task<AccountResponse> UnfreezeAsync(Guid accountId, Guid actorId, CancellationToken cancellationToken);

    Task<PagedResult<AccountResponse>> AdminListAsync(string? query, PageRequest page, CancellationToken cancellationToken);

    Task<BeneficiaryResponse> AddBeneficiaryAsync(Guid ownerCustomerId, CreateBeneficiaryRequest request, CancellationToken cancellationToken);

    Task<IReadOnlyList<BeneficiaryResponse>> ListBeneficiariesAsync(Guid ownerCustomerId, CancellationToken cancellationToken);

    Task ApplyProjectedBalanceAsync(Guid accountId, decimal balance, CancellationToken cancellationToken);

    Task<AccountSnapshot> RequireSnapshotAsync(Guid accountId, CancellationToken cancellationToken);
}

public sealed record AccountSnapshot(
    Guid AccountId,
    Guid CustomerId,
    string AccountNumber,
    string SortCode,
    string Currency,
    string Status,
    string AccountType,
    decimal ProjectedBalance,
    bool CanSend,
    bool CanReceive);
