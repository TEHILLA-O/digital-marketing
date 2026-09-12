using System.Security.Claims;

using LedgerX.Accounts.Application;
using LedgerX.Accounts.Infrastructure;
using LedgerX.Contracts.Api;
using LedgerX.Eventing;
using LedgerX.ServiceDefaults;
using LedgerX.SharedKernel.Paging;
using LedgerX.SharedKernel.Security;

using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);
builder.AddLedgerXDefaults("accounts-api");
builder.AddLedgerXJwtAuth();

builder.Services.AddDbContext<AccountsDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("accounts")));
builder.Services.AddScoped<IAccountService, AccountService>();
builder.Services.AddScoped<AccountsSeeder>();
builder.Services.AddLedgerXEventing<AccountsDbContext>(builder.Configuration);

var app = builder.Build();
app.MapLedgerXDefaults("accounts-api");

if (app.Environment.IsDevelopment() || app.Configuration.GetValue("LedgerX:Seed:Enabled", false))
{
    using var scope = app.Services.CreateScope();
    await scope.ServiceProvider.GetRequiredService<AccountsSeeder>().SeedAsync(CancellationToken.None);
}

Guid CustomerId(ClaimsPrincipal user) =>
    Guid.Parse(user.FindFirstValue(LedgerClaims.CustomerId)
               ?? throw new InvalidOperationException("Customer claim missing."));

Guid UserId(ClaimsPrincipal user) => Guid.Parse(user.FindFirstValue(ClaimTypes.NameIdentifier)!);

bool Staff(ClaimsPrincipal user) => user.IsInRole(Roles.Administrator) || user.IsInRole(Roles.SupportAgent)
                                    || user.IsInRole(Roles.FinanceOperator) || user.IsInRole(Roles.Auditor);

app.MapPost("/api/accounts", async (OpenAccountRequest request, ClaimsPrincipal user, IAccountService accounts, CancellationToken ct) =>
        Results.Ok(await accounts.OpenAsync(CustomerId(user), request, ct)))
    .RequireAuthorization(Policies.CustomerOnly);

app.MapGet("/api/accounts", async (ClaimsPrincipal user, IAccountService accounts, CancellationToken ct) =>
        Results.Ok(await accounts.ListForCustomerAsync(CustomerId(user), ct)))
    .RequireAuthorization(Policies.CustomerOnly);

app.MapGet("/api/accounts/{accountId:guid}", async (Guid accountId, ClaimsPrincipal user, IAccountService accounts, CancellationToken ct) =>
{
    Guid? customer = user.IsInRole(Roles.Customer) ? CustomerId(user) : null;
    return Results.Ok(await accounts.GetAsync(accountId, customer, Staff(user), ct));
}).RequireAuthorization();

app.MapPost("/api/beneficiaries", async (CreateBeneficiaryRequest request, ClaimsPrincipal user, IAccountService accounts, CancellationToken ct) =>
        Results.Ok(await accounts.AddBeneficiaryAsync(CustomerId(user), request, ct)))
    .RequireAuthorization(Policies.CustomerOnly);

app.MapGet("/api/beneficiaries", async (ClaimsPrincipal user, IAccountService accounts, CancellationToken ct) =>
        Results.Ok(await accounts.ListBeneficiariesAsync(CustomerId(user), ct)))
    .RequireAuthorization(Policies.CustomerOnly);

app.MapGet("/api/internal/accounts/{accountId:guid}", async (Guid accountId, IAccountService accounts, CancellationToken ct) =>
        Results.Ok(await accounts.RequireSnapshotAsync(accountId, ct)))
    .AllowAnonymous();

app.MapPost("/api/internal/accounts/{accountId:guid}/projection", async (Guid accountId, ProjectionRequest request, IAccountService accounts, CancellationToken ct) =>
{
    await accounts.ApplyProjectedBalanceAsync(accountId, request.Balance, ct);
    return Results.NoContent();
}).AllowAnonymous();

app.MapGet("/api/admin/accounts", async (string? q, int page, int pageSize, IAccountService accounts, CancellationToken ct) =>
        Results.Ok(await accounts.AdminListAsync(q, new PageRequest(page == 0 ? 1 : page, pageSize == 0 ? 25 : pageSize), ct)))
    .RequireAuthorization(Policies.Staff);

app.MapPost("/api/admin/accounts/{accountId:guid}/freeze", async (Guid accountId, FreezeRequest request, ClaimsPrincipal user, IAccountService accounts, CancellationToken ct) =>
        Results.Ok(await accounts.FreezeAsync(accountId, UserId(user), request.Reason, ct)))
    .RequireAuthorization(Policies.CanFreezeAccounts);

app.MapPost("/api/admin/accounts/{accountId:guid}/unfreeze", async (Guid accountId, ClaimsPrincipal user, IAccountService accounts, CancellationToken ct) =>
        Results.Ok(await accounts.UnfreezeAsync(accountId, UserId(user), ct)))
    .RequireAuthorization(Policies.CanFreezeAccounts);

app.Run();

public sealed record FreezeRequest(string Reason);

public sealed record ProjectionRequest(decimal Balance);

public partial class Program;
