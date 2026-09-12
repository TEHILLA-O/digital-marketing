using System.Text.Json;

using Confluent.Kafka;

using LedgerX.Audit.Worker;
using LedgerX.Contracts.Events;
using LedgerX.Eventing.Consumers;
using LedgerX.Eventing.Kafka;
using LedgerX.Eventing.Outbox;
using LedgerX.ServiceDefaults;
using LedgerX.SharedKernel.Paging;
using LedgerX.SharedKernel.Security;

using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);
builder.AddLedgerXDefaults("audit-worker");
builder.AddLedgerXJwtAuth();
builder.Services.Configure<KafkaOptions>(builder.Configuration.GetSection(KafkaOptions.SectionName));
builder.Services.AddDbContext<AuditDbContext>(o =>
    o.UseNpgsql(builder.Configuration.GetConnectionString("audit")));
builder.Services.AddHostedService<AuditConsumer>();

var app = builder.Build();
app.MapLedgerXDefaults("audit-worker");

if (app.Environment.IsDevelopment() || app.Configuration.GetValue("LedgerX:Seed:Enabled", false))
{
    using var scope = app.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<AuditDbContext>();
    await LedgerX.Eventing.Persistence.DatabaseStartup.MigrateOrCreateAsync(db);
    if (!await db.AuditRecords.AnyAsync())
    {
        db.AuditRecords.Add(new AuditRecord
        {
            ActorId = "system",
            Action = "SYSTEM_SEEDED",
            EntityType = "Platform",
            EntityId = "ledgerx",
            CorrelationId = "seed",
            NewValues = "Demo environment initialised"
        });
        await db.SaveChangesAsync();
    }
}

app.MapGet("/api/admin/audit", async (string? action, int page, int pageSize, AuditDbContext db, CancellationToken ct) =>
{
    var q = db.AuditRecords.AsNoTracking().AsQueryable();
    if (!string.IsNullOrWhiteSpace(action))
    {
        q = q.Where(a => a.Action == action);
    }

    var take = Math.Clamp(pageSize == 0 ? 25 : pageSize, 1, 200);
    var current = page == 0 ? 1 : page;
    var total = await q.CountAsync(ct);
    var items = await q.OrderByDescending(a => a.Timestamp).Skip((current - 1) * take).Take(take)
        .Select(a => new LedgerX.Contracts.Api.AuditRecordResponse(a.Id, a.ActorId, a.Action, a.EntityType, a.EntityId, a.Timestamp, a.CorrelationId, a.OldValues, a.NewValues, a.RemoteIp))
        .ToListAsync(ct);
    return Results.Ok(new PagedResult<LedgerX.Contracts.Api.AuditRecordResponse>(items, total, current, take));
}).RequireAuthorization(Policies.Staff);

app.Run();

public sealed class AuditConsumer(IServiceScopeFactory scopes, Microsoft.Extensions.Options.IOptions<KafkaOptions> options, ILogger<AuditConsumer> logger)
    : KafkaConsumerService(options, logger, "ledgerx-audit", [KafkaTopics.Security, KafkaTopics.Accounts, KafkaTopics.Identity])
{
    protected override async Task HandleAsync(string payload, string topic, CancellationToken cancellationToken)
    {
        await using var scope = scopes.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AuditDbContext>();
        using var doc = JsonDocument.Parse(payload);
        var root = doc.RootElement;
        var eventId = root.TryGetProperty("eventId", out var idEl) && idEl.TryGetGuid(out var guid) ? guid : Guid.NewGuid();
        if (!await InboxGuard.TryBeginAsync(db, eventId, "audit", cancellationToken))
        {
            return;
        }

        var eventType = root.GetProperty("eventType").GetString() ?? "Unknown";
        var correlation = root.TryGetProperty("correlationId", out var c) ? c.GetString() ?? "unknown" : "unknown";
        var action = eventType switch
        {
            EventTypes.AccountFrozen => "ACCOUNT_FROZEN",
            EventTypes.AccountUnfrozen => "ACCOUNT_UNFROZEN",
            EventTypes.SecurityEventRaised => root.GetProperty("payload").TryGetProperty("action", out var a) ? a.GetString() ?? eventType : eventType,
            EventTypes.CustomerRegistered => "CUSTOMER_REGISTERED",
            _ => eventType
        };

        db.AuditRecords.Add(new AuditRecord
        {
            ActorId = Try(root, "payload", "actorId") ?? "system",
            Action = action,
            EntityType = Try(root, "payload", "entityType") ?? eventType,
            EntityId = Try(root, "payload", "entityId") ?? Try(root, "payload", "accountId") ?? Try(root, "payload", "customerId"),
            CorrelationId = correlation,
            NewValues = Sanitize(root.GetProperty("payload").GetRawText()),
            Timestamp = root.TryGetProperty("occurredAt", out var t) && t.TryGetDateTimeOffset(out var dto) ? dto : DateTimeOffset.UtcNow
        });
        await db.SaveChangesAsync(cancellationToken);
    }

    private static string? Try(JsonElement root, string a, string b) =>
        root.TryGetProperty(a, out var p) && p.TryGetProperty(b, out var v) ? v.GetString() : null;

    private static string Sanitize(string json) =>
        json.Replace("password", "***", StringComparison.OrdinalIgnoreCase)
            .Replace("Password", "***");
}

public partial class Program;
