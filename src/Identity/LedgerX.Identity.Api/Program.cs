using System.Security.Claims;

using LedgerX.Contracts.Api;
using LedgerX.Eventing;
using LedgerX.Identity.Application;
using LedgerX.Identity.Infrastructure;
using LedgerX.ServiceDefaults;
using LedgerX.SharedKernel.Paging;
using LedgerX.SharedKernel.Security;

using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);
builder.AddLedgerXDefaults("identity-api");
builder.AddLedgerXJwtAuth();

builder.Services.AddDbContext<IdentityDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("identity")));

builder.Services
    .AddIdentityCore<ApplicationUser>(options =>
    {
        options.Password.RequiredLength = 12;
        options.Password.RequireDigit = true;
        options.Password.RequireUppercase = true;
        options.Password.RequireLowercase = true;
        options.Password.RequireNonAlphanumeric = false;
        options.User.RequireUniqueEmail = true;
    })
    .AddRoles<IdentityRole<Guid>>()
    .AddEntityFrameworkStores<IdentityDbContext>();

builder.Services.AddScoped<IIdentityService, IdentityService>();
builder.Services.AddScoped<IdentitySeeder>();
builder.Services.AddLedgerXEventing<IdentityDbContext>(builder.Configuration);

var app = builder.Build();
app.MapLedgerXDefaults("identity-api");

if (app.Environment.IsDevelopment() || app.Configuration.GetValue("LedgerX:Seed:Enabled", false))
{
    using var scope = app.Services.CreateScope();
    await scope.ServiceProvider.GetRequiredService<IdentitySeeder>().SeedAsync(CancellationToken.None);
}

var auth = app.MapGroup("/api/auth").RequireRateLimiting("fixed");
auth.MapPost("/register", async (RegisterRequest request, IIdentityService identity, CancellationToken ct) =>
    Results.Ok(await identity.RegisterAsync(request, ct)));
auth.MapPost("/login", async (LoginRequest request, IIdentityService identity, CancellationToken ct) =>
    Results.Ok(await identity.LoginAsync(request, ct)));

app.MapGet("/api/me", async (ClaimsPrincipal user, IIdentityService identity, CancellationToken ct) =>
{
    var id = Guid.Parse(user.FindFirstValue(ClaimTypes.NameIdentifier)!);
    return Results.Ok(await identity.GetProfileAsync(id, ct));
}).RequireAuthorization();

app.MapPut("/api/me", async (UpdateProfileRequest request, ClaimsPrincipal user, IIdentityService identity, CancellationToken ct) =>
{
    var id = Guid.Parse(user.FindFirstValue(ClaimTypes.NameIdentifier)!);
    return Results.Ok(await identity.UpdateProfileAsync(id, request, ct));
}).RequireAuthorization();

var admin = app.MapGroup("/api/admin/customers").RequireAuthorization(Policies.Staff);
admin.MapGet("/", async (string? q, int page, int pageSize, IIdentityService identity, CancellationToken ct) =>
    Results.Ok(await identity.SearchCustomersAsync(q, new PageRequest(page == 0 ? 1 : page, pageSize == 0 ? 25 : pageSize), ct)));
admin.MapGet("/{customerId:guid}", async (Guid customerId, IIdentityService identity, CancellationToken ct) =>
    Results.Ok(await identity.GetCustomerAsync(customerId, ct)));
admin.MapPost("/{customerId:guid}/status", async (Guid customerId, ChangeStatusRequest request, ClaimsPrincipal user, IIdentityService identity, CancellationToken ct) =>
{
    var actor = Guid.Parse(user.FindFirstValue(ClaimTypes.NameIdentifier)!);
    await identity.ChangeCustomerStatusAsync(customerId, request.Status, actor, ct);
    return Results.NoContent();
}).RequireAuthorization(Policies.Administrator);

app.Run();

public sealed record ChangeStatusRequest(string Status);

public partial class Program;
