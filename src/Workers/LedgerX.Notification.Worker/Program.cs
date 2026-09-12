using System.Text.Json;

using LedgerX.Contracts.Events;
using LedgerX.Eventing.Consumers;
using LedgerX.Eventing.Kafka;
using LedgerX.ServiceDefaults;

var builder = WebApplication.CreateBuilder(args);
builder.AddLedgerXDefaults("notification-worker");
builder.AddLedgerXJwtAuth();
builder.Services.Configure<KafkaOptions>(builder.Configuration.GetSection(KafkaOptions.SectionName));
builder.Services.AddSingleton<NotificationStore>();
builder.Services.AddHostedService<NotificationConsumer>();

var app = builder.Build();
app.MapLedgerXDefaults("notification-worker");
app.MapGet("/api/notifications", (NotificationStore store) => Results.Ok(store.Latest()));
app.Run();

public sealed class NotificationStore
{
    private readonly Queue<Notification> _items = new();
    private readonly object _gate = new();

    public void Add(Notification notification)
    {
        lock (_gate)
        {
            _items.Enqueue(notification);
            while (_items.Count > 200)
            {
                _items.Dequeue();
            }
        }
    }

    public IReadOnlyList<Notification> Latest()
    {
        lock (_gate)
        {
            return _items.Reverse().Take(50).ToList();
        }
    }
}

public sealed record Notification(DateTimeOffset At, string EventType, string Message);

public sealed class NotificationConsumer(NotificationStore store, Microsoft.Extensions.Options.IOptions<KafkaOptions> options, ILogger<NotificationConsumer> logger)
    : KafkaConsumerService(options, logger, "ledgerx-notifications", [KafkaTopics.Payments, KafkaTopics.Accounts, KafkaTopics.Identity])
{
    protected override Task HandleAsync(string payload, string topic, CancellationToken cancellationToken)
    {
        using var doc = JsonDocument.Parse(payload);
        var type = doc.RootElement.TryGetProperty("eventType", out var t) ? t.GetString() ?? topic : topic;
        store.Add(new Notification(DateTimeOffset.UtcNow, type, $"Processed {type} from {topic}."));
        logger.LogInformation("Notification captured for {EventType}", type);
        return Task.CompletedTask;
    }
}

public partial class Program;
