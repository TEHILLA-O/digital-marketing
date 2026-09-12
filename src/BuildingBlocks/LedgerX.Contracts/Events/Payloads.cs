namespace LedgerX.Contracts.Events;

public sealed record CustomerRegisteredV1(
    Guid CustomerId,
    Guid UserId,
    string Email,
    string FirstName,
    string LastName);

public sealed record AccountCreatedV1(
    Guid AccountId,
    Guid CustomerId,
    string AccountNumber,
    string SortCode,
    string Currency,
    string AccountType);

public sealed record AccountFrozenV1(
    Guid AccountId,
    Guid CustomerId,
    Guid ActorId,
    string Reason);

public sealed record AccountUnfrozenV1(
    Guid AccountId,
    Guid CustomerId,
    Guid ActorId);

public sealed record TransferCreatedV1(
    Guid TransferId,
    string Reference,
    string Kind,
    Guid SenderCustomerId,
    Guid SourceAccountId,
    Guid? DestinationAccountId,
    decimal Amount,
    string Currency,
    string IdempotencyKey);

public sealed record TransferCompletedV1(
    Guid TransferId,
    string Reference,
    Guid? JournalId,
    Guid SourceAccountId,
    Guid? DestinationAccountId,
    decimal Amount,
    string Currency);

public sealed record TransferFailedV1(
    Guid TransferId,
    string Reference,
    string Status,
    string Reason);

public sealed record FundsDepositedV1(
    Guid TransferId,
    Guid AccountId,
    Guid CustomerId,
    decimal Amount,
    string Currency,
    Guid JournalId);

public sealed record FundsWithdrawnV1(
    Guid TransferId,
    Guid AccountId,
    Guid CustomerId,
    decimal Amount,
    string Currency,
    Guid JournalId);

public sealed record JournalPostedV1(
    Guid JournalId,
    string Reference,
    string Currency,
    decimal TotalDebits,
    decimal TotalCredits,
    string? SourceType,
    Guid? SourceId,
    IReadOnlyList<JournalLineV1> Lines);

public sealed record JournalLineV1(
    Guid LineId,
    Guid LedgerAccountId,
    string Side,
    decimal Amount,
    string Narrative);

public sealed record SecurityEventRaisedV1(
    string Action,
    string ActorId,
    string? EntityType,
    string? EntityId,
    string? Detail);
