using LedgerX.Contracts.Api;
using LedgerX.Eventing;
using LedgerX.Ledger.Application;
using LedgerX.Ledger.Infrastructure;
using LedgerX.ServiceDefaults;
using LedgerX.SharedKernel.Paging;
using LedgerX.SharedKernel.Security;

using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);
builder.AddLedgerXDefaults("ledger-api");
builder.AddLedgerXJwtAuth();

builder.Services.AddDbContext<LedgerDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("ledger"))
        .UseSnakeCaseNamingConvention());
builder.Services.AddScoped<ILedgerService, LedgerService>();
builder.Services.AddScoped<LedgerSeeder>();
builder.Services.AddLedgerXEventing<LedgerDbContext>(builder.Configuration);

var app = builder.Build();
app.MapLedgerXDefaults("ledger-api");

if (app.Environment.IsDevelopment() || app.Configuration.GetValue("LedgerX:Seed:Enabled", false))
{
    using var scope = app.Services.CreateScope();
    await scope.ServiceProvider.GetRequiredService<LedgerSeeder>().SeedAsync(CancellationToken.None);
}

app.MapPost("/api/journals", async (PostJournalRequest request, ILedgerService ledger, CancellationToken ct) =>
        Results.Ok(await ledger.PostAsync(request, ct)))
    .RequireAuthorization(Policies.CanInspectLedger);

app.MapPost("/api/journals/{journalId:guid}/reverse", async (Guid journalId, ILedgerService ledger, CancellationToken ct) =>
        Results.Ok(await ledger.ReverseAsync(journalId, ct)))
    .RequireAuthorization(Policies.FinanceOperator);

app.MapGet("/api/journals/{journalId:guid}", async (Guid journalId, ILedgerService ledger, CancellationToken ct) =>
        Results.Ok(await ledger.GetJournalAsync(journalId, ct)))
    .RequireAuthorization(Policies.CanInspectLedger);

app.MapGet("/api/journals", async (string? q, int page, int pageSize, ILedgerService ledger, CancellationToken ct) =>
        Results.Ok(await ledger.ListJournalsAsync(q, new PageRequest(page == 0 ? 1 : page, pageSize == 0 ? 25 : pageSize), ct)))
    .RequireAuthorization(Policies.CanInspectLedger);

app.MapGet("/api/ledger-accounts", async (ILedgerService ledger, CancellationToken ct) =>
        Results.Ok(await ledger.ListAccountsAsync(ct)))
    .RequireAuthorization(Policies.CanInspectLedger);

app.MapGet("/api/statements/{accountId:guid}", async (Guid accountId, DateTimeOffset? from, DateTimeOffset? to, ILedgerService ledger, CancellationToken ct) =>
        Results.Ok(await ledger.GetStatementAsync(accountId, from ?? DateTimeOffset.UtcNow.AddYears(-1), to ?? DateTimeOffset.UtcNow, ct)))
    .RequireAuthorization();

app.MapPost("/api/internal/ledger-accounts", async (EnsureCustomerLedgerAccountRequest request, ILedgerService ledger, CancellationToken ct) =>
        Results.Ok(await ledger.EnsureCustomerAccountAsync(request, ct)))
    .AllowAnonymous();

app.MapPost("/api/internal/transfers", async (InternalTransferPost request, ILedgerService ledger, CancellationToken ct) =>
        Results.Ok(await ledger.PostTransferAsync(request.SourceAccountId, request.DestinationAccountId, request.Amount, request.Currency, request.Reference, request.IdempotencyKey, request.SourceId, ct)))
    .AllowAnonymous();

app.MapPost("/api/internal/deposits", async (InternalCashPost request, ILedgerService ledger, CancellationToken ct) =>
        Results.Ok(await ledger.PostDepositAsync(request.AccountId, request.Amount, request.Currency, request.Reference, request.IdempotencyKey, request.SourceId, ct)))
    .AllowAnonymous();

app.MapPost("/api/internal/withdrawals", async (InternalCashPost request, ILedgerService ledger, CancellationToken ct) =>
        Results.Ok(await ledger.PostWithdrawalAsync(request.AccountId, request.Amount, request.Currency, request.Reference, request.IdempotencyKey, request.SourceId, ct)))
    .AllowAnonymous();

app.MapGet("/api/internal/balances/{accountId:guid}", async (Guid accountId, ILedgerService ledger, CancellationToken ct) =>
        Results.Ok(new { accountId, balance = await ledger.GetCustomerBalanceAsync(accountId, ct) }))
    .AllowAnonymous();

app.MapGet("/api/internal/ledger-accounts", async (ILedgerService ledger, CancellationToken ct) =>
        Results.Ok(await ledger.ListAccountsAsync(ct)))
    .AllowAnonymous();

app.Run();

public sealed record InternalTransferPost(Guid SourceAccountId, Guid DestinationAccountId, decimal Amount, string Currency, string Reference, string IdempotencyKey, Guid SourceId);

public sealed record InternalCashPost(Guid AccountId, decimal Amount, string Currency, string Reference, string IdempotencyKey, Guid SourceId);

public partial class Program;
