using LedgerX.Contracts.Api;
using LedgerX.SharedKernel.Paging;

namespace LedgerX.Ledger.Application;

public interface ILedgerService
{
    Task<JournalResponse> PostAsync(PostJournalRequest request, CancellationToken cancellationToken);

    Task<JournalResponse> PostTransferAsync(Guid sourceBankAccountId, Guid destinationBankAccountId, decimal amount, string currency, string reference, string idempotencyKey, Guid sourceId, CancellationToken cancellationToken);

    Task<JournalResponse> PostDepositAsync(Guid bankAccountId, decimal amount, string currency, string reference, string idempotencyKey, Guid sourceId, CancellationToken cancellationToken);

    Task<JournalResponse> PostWithdrawalAsync(Guid bankAccountId, decimal amount, string currency, string reference, string idempotencyKey, Guid sourceId, CancellationToken cancellationToken);

    Task<JournalResponse> ReverseAsync(Guid journalId, CancellationToken cancellationToken);

    Task<JournalResponse> GetJournalAsync(Guid journalId, CancellationToken cancellationToken);

    Task<PagedResult<JournalResponse>> ListJournalsAsync(string? query, PageRequest page, CancellationToken cancellationToken);

    Task<IReadOnlyList<LedgerAccountResponse>> ListAccountsAsync(CancellationToken cancellationToken);

    Task<LedgerAccountResponse> EnsureCustomerAccountAsync(EnsureCustomerLedgerAccountRequest request, CancellationToken cancellationToken);

    Task<decimal> GetCustomerBalanceAsync(Guid bankAccountId, CancellationToken cancellationToken);

    Task<StatementResponse> GetStatementAsync(Guid bankAccountId, DateTimeOffset from, DateTimeOffset to, CancellationToken cancellationToken);
}
