namespace LedgerX.Contracts.Events;

public sealed record IntegrationEvent<TPayload>
    where TPayload : class
{
    public required Guid EventId { get; init; }

    public required string EventType { get; init; }

    public required DateTimeOffset OccurredAt { get; init; }

    public required string CorrelationId { get; init; }

    public string? CausationId { get; init; }

    public required int Version { get; init; }

    public required TPayload Payload { get; init; }

    public static IntegrationEvent<TPayload> Create(
        string eventType,
        TPayload payload,
        string correlationId,
        string? causationId = null,
        int version = 1,
        DateTimeOffset? occurredAt = null) =>
        new()
        {
            EventId = Guid.NewGuid(),
            EventType = eventType,
            OccurredAt = occurredAt ?? DateTimeOffset.UtcNow,
            CorrelationId = correlationId,
            CausationId = causationId,
            Version = version,
            Payload = payload
        };
}

public static class IntegrationEvents
{
    public static IntegrationEvent<TPayload> Create<TPayload>(
        string eventType,
        TPayload payload,
        string correlationId,
        string? causationId = null,
        int version = 1,
        DateTimeOffset? occurredAt = null)
        where TPayload : class =>
        IntegrationEvent<TPayload>.Create(eventType, payload, correlationId, causationId, version, occurredAt);
}

public static class EventTypes
{
    public const string CustomerRegistered = "CustomerRegistered";
    public const string AccountCreated = "AccountCreated";
    public const string AccountFrozen = "AccountFrozen";
    public const string AccountUnfrozen = "AccountUnfrozen";
    public const string TransferCreated = "TransferCreated";
    public const string TransferCompleted = "TransferCompleted";
    public const string TransferFailed = "TransferFailed";
    public const string FundsDeposited = "FundsDeposited";
    public const string FundsWithdrawn = "FundsWithdrawn";
    public const string JournalPosted = "JournalPosted";
    public const string SecurityEventRaised = "SecurityEventRaised";
}

public static class KafkaTopics
{
    public const string Identity = "ledgerx.identity.events";
    public const string Accounts = "ledgerx.accounts.events";
    public const string Payments = "ledgerx.payments.events";
    public const string Ledger = "ledgerx.ledger.events";
    public const string Audit = "ledgerx.audit.events";
    public const string Security = "ledgerx.security.events";
}
