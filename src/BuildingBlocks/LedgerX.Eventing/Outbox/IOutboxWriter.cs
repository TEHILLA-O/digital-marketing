using System.Text.Json;

using LedgerX.Contracts.Events;

namespace LedgerX.Eventing.Outbox;

public interface IOutboxWriter
{
    Task EnqueueAsync<TPayload>(
        string topic,
        IntegrationEvent<TPayload> integrationEvent,
        CancellationToken cancellationToken)
        where TPayload : class;
}

public static class EventSerializer
{
    public static readonly JsonSerializerOptions Options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false
    };

    public static string Serialize<T>(T value) => JsonSerializer.Serialize(value, Options);

    public static T? Deserialize<T>(string json) => JsonSerializer.Deserialize<T>(json, Options);
}
