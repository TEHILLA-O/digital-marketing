using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

using LedgerX.Contracts.Api;
using LedgerX.SharedKernel.Paging;

namespace LedgerX.Web.Services;

public sealed class LedgerXApi(IHttpClientFactory http, SessionState session)
{
    private static readonly JsonSerializerOptions Json = new() { PropertyNameCaseInsensitive = true };

    public Task<TokenResponse?> LoginAsync(LoginRequest request, CancellationToken ct) =>
        PostAnon<TokenResponse>("identity", "/api/auth/login", request, ct);

    public Task<TokenResponse?> RegisterAsync(RegisterRequest request, CancellationToken ct) =>
        PostAnon<TokenResponse>("identity", "/api/auth/register", request, ct);

    public Task<ProfileResponse?> MeAsync(CancellationToken ct) => Get<ProfileResponse>("identity", "/api/me", ct);

    public Task<List<AccountResponse>?> AccountsAsync(CancellationToken ct) => Get<List<AccountResponse>>("accounts", "/api/accounts", ct);

    public Task<AccountResponse?> AccountAsync(Guid id, CancellationToken ct) => Get<AccountResponse>("accounts", $"/api/accounts/{id}", ct);

    public Task<AccountResponse?> OpenAccountAsync(OpenAccountRequest request, CancellationToken ct) =>
        Post<AccountResponse>("accounts", "/api/accounts", request, ct);

    public Task<List<BeneficiaryResponse>?> BeneficiariesAsync(CancellationToken ct) => Get<List<BeneficiaryResponse>>("accounts", "/api/beneficiaries", ct);

    public Task<BeneficiaryResponse?> AddBeneficiaryAsync(CreateBeneficiaryRequest request, CancellationToken ct) =>
        Post<BeneficiaryResponse>("accounts", "/api/beneficiaries", request, ct);

    public Task<TransferResponse?> TransferAsync(CreateTransferRequest request, string idempotencyKey, CancellationToken ct) =>
        Post<TransferResponse>("payments", "/api/transfers", request, ct, idempotencyKey);

    public Task<TransferResponse?> DepositAsync(CreateDepositRequest request, string idempotencyKey, CancellationToken ct) =>
        Post<TransferResponse>("payments", "/api/deposits", request, ct, idempotencyKey);

    public Task<TransferResponse?> WithdrawAsync(CreateWithdrawalRequest request, string idempotencyKey, CancellationToken ct) =>
        Post<TransferResponse>("payments", "/api/withdrawals", request, ct, idempotencyKey);

    public Task<PagedResult<TransferResponse>?> TransfersAsync(string? status, CancellationToken ct) =>
        Get<PagedResult<TransferResponse>>("payments", $"/api/transfers?page=1&pageSize=50&status={status}", ct);

    public Task<StatementResponse?> StatementAsync(Guid accountId, CancellationToken ct) =>
        Get<StatementResponse>("ledger", $"/api/statements/{accountId}", ct);

    public Task<PagedResult<CustomerAdminResponse>?> CustomersAsync(string? q, CancellationToken ct) =>
        Get<PagedResult<CustomerAdminResponse>>("identity", $"/api/admin/customers?q={q}&page=1&pageSize=50", ct);

    public Task<PagedResult<AccountResponse>?> AdminAccountsAsync(CancellationToken ct) =>
        Get<PagedResult<AccountResponse>>("accounts", "/api/admin/accounts?page=1&pageSize=50", ct);

    public Task<AccountResponse?> FreezeAsync(Guid id, string reason, CancellationToken ct) =>
        Post<AccountResponse>("accounts", $"/api/admin/accounts/{id}/freeze", new FreezeBody(reason), ct);

    public Task<AccountResponse?> UnfreezeAsync(Guid id, CancellationToken ct) =>
        Post<AccountResponse>("accounts", $"/api/admin/accounts/{id}/unfreeze", new { }, ct);

    public Task<PagedResult<TransferResponse>?> AdminTransfersAsync(string? status, CancellationToken ct) =>
        Get<PagedResult<TransferResponse>>("payments", $"/api/admin/transfers?status={status}&page=1&pageSize=50", ct);

    public Task<PagedResult<JournalResponse>?> JournalsAsync(CancellationToken ct) =>
        Get<PagedResult<JournalResponse>>("ledger", "/api/journals?page=1&pageSize=50", ct);

    public Task<JournalResponse?> JournalAsync(Guid id, CancellationToken ct) =>
        Get<JournalResponse>("ledger", $"/api/journals/{id}", ct);

    public Task<List<LedgerAccountResponse>?> LedgerAccountsAsync(CancellationToken ct) =>
        Get<List<LedgerAccountResponse>>("ledger", "/api/ledger-accounts", ct);

    public Task<PagedResult<AuditRecordResponse>?> AuditAsync(CancellationToken ct) =>
        Get<PagedResult<AuditRecordResponse>>("audit", "/api/admin/audit?page=1&pageSize=50", ct);

    public async Task<string?> HealthAsync(string name, string url, CancellationToken ct)
    {
        try
        {
            var client = http.CreateClient(name);
            var response = await client.GetAsync(url, ct);
            return response.IsSuccessStatusCode ? "Healthy" : $"HTTP {(int)response.StatusCode}";
        }
        catch (Exception ex)
        {
            return ex.Message;
        }
    }

    private async Task<T?> Get<T>(string clientName, string path, CancellationToken ct)
    {
        var client = Create(clientName);
        var response = await client.GetAsync(path, ct);
        if (!response.IsSuccessStatusCode)
        {
            return default;
        }

        return await response.Content.ReadFromJsonAsync<T>(Json, ct);
    }

    private async Task<T?> Post<T>(string clientName, string path, object body, CancellationToken ct, string? idempotency = null)
    {
        var client = Create(clientName);
        using var request = new HttpRequestMessage(HttpMethod.Post, path) { Content = JsonContent.Create(body) };
        if (!string.IsNullOrWhiteSpace(idempotency))
        {
            request.Headers.TryAddWithoutValidation("Idempotency-Key", idempotency);
        }

        var response = await client.SendAsync(request, ct);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<T>(Json, ct);
    }

    private async Task<T?> PostAnon<T>(string clientName, string path, object body, CancellationToken ct)
    {
        var client = http.CreateClient(clientName);
        var response = await client.PostAsJsonAsync(path, body, ct);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<T>(Json, ct);
    }

    private HttpClient Create(string name)
    {
        var client = http.CreateClient(name);
        if (session.AccessToken is { } token)
        {
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        }

        return client;
    }

    private sealed record FreezeBody(string Reason);
}
