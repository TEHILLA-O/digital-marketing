using System.Net.Http.Json;
using System.Text.Json;

using LedgerX.Contracts.Events;
using LedgerX.Eventing.Consumers;
using LedgerX.Eventing.Kafka;
using LedgerX.ServiceDefaults;

using StackExchange.Redis;

var builder = WebApplication.CreateBuilder(args);
builder.AddLedgerXDefaults("projection-worker");
builder.AddLedgerXJwtAuth();
builder.Services.Configure<KafkaOptions>(builder.Configuration.GetSection(KafkaOptions.SectionName));
builder.Services.AddSingleton<IConnectionMultiplexer>(_ =>
    ConnectionMultiplexer.Connect(builder.Configuration.GetConnectionString("redis") ?? "localhost:6379"));
builder.Services.AddHttpClient("accounts", c => c.BaseAddress = new Uri(builder.Configuration["Services:Accounts"] ?? "http://localhost:5102"));
builder.Services.AddHttpClient("ledger", c => c.BaseAddress = new Uri(builder.Configuration["Services:Ledger"] ?? "http://localhost:5104"));
builder.Services.AddHostedService<ProjectionConsumer>();

var app = builder.Build();
app.MapLedgerXDefaults("projection-worker");
app.Run();

public sealed class ProjectionConsumer(
    IHttpClientFactory http,
    IConnectionMultiplexer redis,
    Microsoft.Extensions.Options.IOptions<KafkaOptions> options,
    ILogger<ProjectionConsumer> logger)
    : KafkaConsumerService(options, logger, "ledgerx-projections", [KafkaTopics.Ledger])
{
    protected override async Task HandleAsync(string payload, string topic, CancellationToken cancellationToken)
    {
        using var doc = JsonDocument.Parse(payload);
        var type = doc.RootElement.GetProperty("eventType").GetString();
        if (type != EventTypes.JournalPosted)
        {
            return;
        }

        var body = JsonSerializer.Deserialize<IntegrationEvent<JournalPostedV1>>(payload, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        if (body?.Payload is null)
        {
            return;
        }

        var ledger = http.CreateClient("ledger");
        var accounts = http.CreateClient("accounts");
        var cache = redis.GetDatabase();

        foreach (var line in body.Payload.Lines)
        {
            var accountResp = await ledger.GetFromJsonAsync<IReadOnlyList<LedgerAccountLite>>("/api/internal/ledger-accounts", cancellationToken);
            var match = accountResp?.FirstOrDefault(a => a.LedgerAccountId == line.LedgerAccountId);
            if (match?.BankAccountId is not { } bankAccountId)
            {
                continue;
            }

            var balanceResp = await ledger.GetFromJsonAsync<BalanceLite>($"/api/internal/balances/{bankAccountId}", cancellationToken);
            if (balanceResp is null)
            {
                continue;
            }

            await accounts.PostAsJsonAsync($"/api/internal/accounts/{bankAccountId}/projection", new { balance = balanceResp.Balance }, cancellationToken);
            await cache.StringSetAsync($"ledgerx:balance:{bankAccountId}", balanceResp.Balance.ToString("F2"), TimeSpan.FromMinutes(5));
        }
    }

    private sealed record LedgerAccountLite(Guid LedgerAccountId, Guid? BankAccountId);

    private sealed record BalanceLite(decimal Balance);
}

public partial class Program;
