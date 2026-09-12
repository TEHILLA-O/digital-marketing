using System.Net.Http.Json;
using System.Text.Json;

using LedgerX.Contracts.Api;
using LedgerX.Payments.Application;
using LedgerX.SharedKernel;

namespace LedgerX.Payments.Infrastructure;

public sealed class AccountsHttpGateway(HttpClient http) : IAccountsGateway
{
    private static readonly JsonSerializerOptions Json = new() { PropertyNameCaseInsensitive = true };

    public async Task<AccountSnapshotDto> GetAccountAsync(Guid accountId, CancellationToken cancellationToken)
    {
        var response = await http.GetAsync($"/api/internal/accounts/{accountId}", cancellationToken).ConfigureAwait(false);
        if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            throw new NotFoundException("BankAccount", accountId);
        }

        response.EnsureSuccessStatusCode();
        var body = await response.Content.ReadFromJsonAsync<AccountSnapshotDto>(Json, cancellationToken).ConfigureAwait(false)
                   ?? throw new DomainException("Accounts service returned an empty snapshot.");
        return body;
    }

    public async Task UpdateProjectedBalanceAsync(Guid accountId, decimal balance, CancellationToken cancellationToken)
    {
        var response = await http.PostAsJsonAsync($"/api/internal/accounts/{accountId}/projection", new { balance }, cancellationToken).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();
    }
}

public sealed class LedgerHttpGateway(HttpClient http) : ILedgerGateway
{
    public async Task EnsureCustomerAccountAsync(Guid bankAccountId, Guid customerId, string currency, string displayName, CancellationToken cancellationToken)
    {
        var response = await http.PostAsJsonAsync("/api/internal/ledger-accounts", new EnsureCustomerLedgerAccountRequest(bankAccountId, customerId, currency, displayName), cancellationToken).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();
    }

    public async Task<JournalResponse> PostTransferAsync(Guid source, Guid destination, decimal amount, string currency, string reference, string idempotencyKey, Guid sourceId, CancellationToken cancellationToken) =>
        await Post<JournalResponse>("/api/internal/transfers", new
        {
            sourceAccountId = source,
            destinationAccountId = destination,
            amount,
            currency,
            reference,
            idempotencyKey,
            sourceId
        }, cancellationToken).ConfigureAwait(false);

    public async Task<JournalResponse> PostDepositAsync(Guid accountId, decimal amount, string currency, string reference, string idempotencyKey, Guid sourceId, CancellationToken cancellationToken) =>
        await Post<JournalResponse>("/api/internal/deposits", new
        {
            accountId,
            amount,
            currency,
            reference,
            idempotencyKey,
            sourceId
        }, cancellationToken).ConfigureAwait(false);

    public async Task<JournalResponse> PostWithdrawalAsync(Guid accountId, decimal amount, string currency, string reference, string idempotencyKey, Guid sourceId, CancellationToken cancellationToken) =>
        await Post<JournalResponse>("/api/internal/withdrawals", new
        {
            accountId,
            amount,
            currency,
            reference,
            idempotencyKey,
            sourceId
        }, cancellationToken).ConfigureAwait(false);

    public async Task<decimal> GetBalanceAsync(Guid accountId, CancellationToken cancellationToken)
    {
        var body = await http.GetFromJsonAsync<BalanceDto>($"/api/internal/balances/{accountId}", cancellationToken).ConfigureAwait(false);
        return body?.Balance ?? 0m;
    }

    private async Task<T> Post<T>(string path, object payload, CancellationToken cancellationToken)
    {
        var response = await http.PostAsJsonAsync(path, payload, cancellationToken).ConfigureAwait(false);
        var text = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
        if (!response.IsSuccessStatusCode)
        {
            if (text.Contains("insufficient_funds", StringComparison.OrdinalIgnoreCase))
            {
                throw new InsufficientFundsException(path);
            }

            throw new DomainException($"Ledger service rejected the request ({(int)response.StatusCode}).");
        }

        return System.Text.Json.JsonSerializer.Deserialize<T>(text, new System.Text.Json.JsonSerializerOptions { PropertyNameCaseInsensitive = true })
               ?? throw new DomainException("Ledger service returned an empty journal.");
    }

    private sealed record BalanceDto(decimal Balance);
}
