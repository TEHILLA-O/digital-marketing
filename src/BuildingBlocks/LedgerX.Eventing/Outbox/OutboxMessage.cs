namespace LedgerX.Eventing.Outbox;

public sealed class OutboxMessage
{
    public Guid Id { get; init; } = Guid.NewGuid();

    public required string EventType { get; init; }

    public required string Topic { get; init; }

    public required string Payload { get; init; }

    public required string CorrelationId { get; init; }

    public string? CausationId { get; init; }

    public DateTimeOffset OccurredAt { get; init; } = DateTimeOffset.UtcNow;

    public DateTimeOffset? ProcessedAt { get; set; }

    public int AttemptCount { get; set; }

    public string? LastError { get; set; }
}

public sealed class InboxMessage
{
    public Guid EventId { get; init; }

    public required string ConsumerName { get; init; }

    public DateTimeOffset ProcessedAt { get; init; } = DateTimeOffset.UtcNow;
}
