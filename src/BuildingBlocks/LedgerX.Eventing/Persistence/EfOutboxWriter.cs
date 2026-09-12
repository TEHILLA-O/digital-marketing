using LedgerX.Contracts.Events;
using LedgerX.Eventing.Outbox;

using Microsoft.EntityFrameworkCore;

namespace LedgerX.Eventing.Persistence;

public sealed class EfOutboxWriter<TContext>(TContext db) : IOutboxWriter
    where TContext : DbContext
{
    public async Task EnqueueAsync<TPayload>(
        string topic,
        IntegrationEvent<TPayload> integrationEvent,
        CancellationToken cancellationToken)
        where TPayload : class
    {
        var set = db.Set<OutboxMessage>();
        set.Add(new OutboxMessage
        {
            Id = integrationEvent.EventId,
            EventType = integrationEvent.EventType,
            Topic = topic,
            Payload = EventSerializer.Serialize(integrationEvent),
            CorrelationId = integrationEvent.CorrelationId,
            CausationId = integrationEvent.CausationId,
            OccurredAt = integrationEvent.OccurredAt
        });

        await Task.CompletedTask.ConfigureAwait(false);
    }
}
