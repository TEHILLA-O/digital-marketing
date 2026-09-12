namespace LedgerX.Audit.Worker;

public sealed class AuditRecord
{
    public Guid Id { get; init; } = Guid.NewGuid();

    public required string ActorId { get; init; }

    public required string Action { get; init; }

    public string? EntityType { get; init; }

    public string? EntityId { get; init; }

    public DateTimeOffset Timestamp { get; init; } = DateTimeOffset.UtcNow;

    public required string CorrelationId { get; init; }

    public string? OldValues { get; init; }

    public string? NewValues { get; init; }

    public string? RemoteIp { get; init; }

    public string? Payload { get; init; }
}
