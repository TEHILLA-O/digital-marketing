using System.Security.Claims;

using LedgerX.Contracts.Api;
using LedgerX.Eventing;
using LedgerX.Payments.Application;
using LedgerX.Payments.Infrastructure;
using LedgerX.ServiceDefaults;
using LedgerX.SharedKernel.Paging;
using LedgerX.SharedKernel.Security;

using Microsoft.EntityFrameworkCore;

using StackExchange.Redis;

var builder = WebApplication.CreateBuilder(args);
builder.AddLedgerXDefaults("payments-api");
builder.AddLedgerXJwtAuth();

builder.Services.AddDbContext<PaymentsDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("payments")));

builder.Services.AddSingleton<IConnectionMultiplexer>(_ =>
    ConnectionMultiplexer.Connect(builder.Configuration.GetConnectionString("redis") ?? "localhost:6379"));

builder.Services.AddHttpClient<IAccountsGateway, AccountsHttpGateway>(client =>
{
    client.BaseAddress = new Uri(builder.Configuration["Services:Accounts"] ?? "http://localhost:5102");
});
builder.Services.AddHttpClient<ILedgerGateway, LedgerHttpGateway>(client =>
{
    client.BaseAddress = new Uri(builder.Configuration["Services:Ledger"] ?? "http://localhost:5104");
});

builder.Services.AddScoped<RedisIdempotencyStore>();
builder.Services.AddScoped<ITransferService, TransferService>();
builder.Services.AddScoped<PaymentsSeeder>();
builder.Services.AddLedgerXEventing<PaymentsDbContext>(builder.Configuration);

var app = builder.Build();
app.MapLedgerXDefaults("payments-api");

if (app.Environment.IsDevelopment() || app.Configuration.GetValue("LedgerX:Seed:Enabled", false))
{
    using var scope = app.Services.CreateScope();
    await scope.ServiceProvider.GetRequiredService<PaymentsSeeder>().SeedAsync(CancellationToken.None);
}

string IdempotencyKey(HttpRequest request) =>
    request.Headers[HeaderNames.IdempotencyKey].FirstOrDefault()
    ?? throw new LedgerX.SharedKernel.DomainException("Idempotency-Key header is required for money-movement requests.", "idempotency_required");

Guid CustomerIdOf(ClaimsPrincipal user) =>
    Guid.TryParse(user.FindFirstValue(LedgerClaims.CustomerId), out var id) ? id : Guid.Empty;

Guid UserId(ClaimsPrincipal user) => Guid.Parse(user.FindFirstValue(ClaimTypes.NameIdentifier)!);

bool Staff(ClaimsPrincipal user) =>
    user.IsInRole(Roles.Administrator) || user.IsInRole(Roles.FinanceOperator) || user.IsInRole(Roles.SupportAgent) || user.IsInRole(Roles.Auditor);

app.MapPost("/api/transfers", async (CreateTransferRequest body, HttpRequest http, ClaimsPrincipal user, ITransferService transfers, CancellationToken ct) =>
        Results.Ok(await transfers.TransferAsync(CustomerIdOf(user), body, IdempotencyKey(http), ct)))
    .RequireAuthorization(Policies.CustomerOnly)
    .RequireRateLimiting("fixed");

app.MapPost("/api/deposits", async (CreateDepositRequest body, HttpRequest http, ClaimsPrincipal user, ITransferService transfers, CancellationToken ct) =>
        Results.Ok(await transfers.DepositAsync(CustomerIdOf(user), body, IdempotencyKey(http), Staff(user), ct)))
    .RequireAuthorization();

app.MapPost("/api/withdrawals", async (CreateWithdrawalRequest body, HttpRequest http, ClaimsPrincipal user, ITransferService transfers, CancellationToken ct) =>
        Results.Ok(await transfers.WithdrawAsync(CustomerIdOf(user), body, IdempotencyKey(http), ct)))
    .RequireAuthorization(Policies.CustomerOnly);

app.MapGet("/api/transfers/{transferId:guid}", async (Guid transferId, ClaimsPrincipal user, ITransferService transfers, CancellationToken ct) =>
        Results.Ok(await transfers.GetAsync(transferId, Staff(user) ? null : CustomerIdOf(user), Staff(user), ct)))
    .RequireAuthorization();

app.MapGet("/api/transfers", async (string? status, int page, int pageSize, ClaimsPrincipal user, ITransferService transfers, CancellationToken ct) =>
        Results.Ok(await transfers.ListAsync(Staff(user) ? null : CustomerIdOf(user), status, new PageRequest(page == 0 ? 1 : page, pageSize == 0 ? 25 : pageSize), ct)))
    .RequireAuthorization();

app.MapGet("/api/admin/transfers", async (string? status, int page, int pageSize, ITransferService transfers, CancellationToken ct) =>
        Results.Ok(await transfers.ListAsync(null, status, new PageRequest(page == 0 ? 1 : page, pageSize == 0 ? 25 : pageSize), ct)))
    .RequireAuthorization(Policies.CanReviewTransfers);

app.MapGet("/api/admin/transfers/failed", async (int page, int pageSize, ITransferService transfers, CancellationToken ct) =>
        Results.Ok(await transfers.ListAsync(null, nameof(LedgerX.Payments.Domain.TransferStatus.Failed), new PageRequest(page == 0 ? 1 : page, pageSize == 0 ? 25 : pageSize), ct)))
    .RequireAuthorization(Policies.CanReviewTransfers);

app.Run();

public partial class Program;
