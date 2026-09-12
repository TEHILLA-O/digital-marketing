using LedgerX.ServiceDefaults;
using LedgerX.Web.Services;

var builder = WebApplication.CreateBuilder(args);
builder.AddLedgerXDefaults("web");
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<SessionState>();
builder.Services.AddScoped<LedgerXApi>();

builder.Services.AddHttpClient("identity", c => c.BaseAddress = new Uri(builder.Configuration["Services:Identity"] ?? "http://localhost:5101"));
builder.Services.AddHttpClient("accounts", c => c.BaseAddress = new Uri(builder.Configuration["Services:Accounts"] ?? "http://localhost:5102"));
builder.Services.AddHttpClient("payments", c => c.BaseAddress = new Uri(builder.Configuration["Services:Payments"] ?? "http://localhost:5103"));
builder.Services.AddHttpClient("ledger", c => c.BaseAddress = new Uri(builder.Configuration["Services:Ledger"] ?? "http://localhost:5104"));
builder.Services.AddHttpClient("audit", c => c.BaseAddress = new Uri(builder.Configuration["Services:Audit"] ?? "http://localhost:5105"));

var app = builder.Build();
app.UseExceptionHandler();
app.UseMiddleware<LedgerX.ServiceDefaults.SecurityHeadersMiddleware>();
app.UseStaticFiles();
app.UseAntiforgery();
app.MapRazorComponents<LedgerX.Web.Components.App>()
    .AddInteractiveServerRenderMode();
app.MapGet("/health/live", () => Results.Ok(new { status = "ok" }));
app.MapGet("/health/ready", () => Results.Ok(new { status = "ok" }));
app.Run();
