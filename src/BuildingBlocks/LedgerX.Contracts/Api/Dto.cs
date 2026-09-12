namespace LedgerX.Contracts.Api;

public sealed record RegisterRequest(string Email, string Password, string FirstName, string LastName);

public sealed record LoginRequest(string Email, string Password);

public sealed record TokenResponse(string AccessToken, DateTimeOffset ExpiresAt, string TokenType, IReadOnlyList<string> Roles, Guid? CustomerId, string DisplayName);

public sealed record ProfileResponse(Guid UserId, Guid? CustomerId, string Email, string FirstName, string LastName, string Status, IReadOnlyList<string> Roles);

public sealed record UpdateProfileRequest(string FirstName, string LastName);

public sealed record OpenAccountRequest(string AccountType, string? Currency, string DisplayName);

public sealed record AccountResponse(
    Guid AccountId,
    Guid CustomerId,
    string AccountNumber,
    string SortCode,
    string Currency,
    string AccountType,
    string Status,
    string DisplayName,
    decimal ProjectedBalance,
    DateTimeOffset CreatedAt);

public sealed record CreateBeneficiaryRequest(string DisplayName, Guid DestinationAccountId);

public sealed record BeneficiaryResponse(Guid BeneficiaryId, string DisplayName, string AccountNumber, string SortCode, string Currency, Guid? LinkedBankAccountId);

public sealed record CreateTransferRequest(Guid SourceAccountId, Guid DestinationAccountId, decimal Amount, string? Currency, string? Description);

public sealed record CreateDepositRequest(Guid AccountId, decimal Amount, string? Currency, string? Description);

public sealed record CreateWithdrawalRequest(Guid AccountId, decimal Amount, string? Currency, string? Description);

public sealed record TransferResponse(
    Guid TransferId,
    string Reference,
    string Kind,
    Guid SourceAccountId,
    Guid? DestinationAccountId,
    decimal Amount,
    string Currency,
    string Description,
    string Status,
    string? FailureReason,
    Guid? JournalId,
    DateTimeOffset CreatedAt,
    DateTimeOffset? CompletedAt);

public sealed record JournalLineResponse(Guid LineId, Guid LedgerAccountId, string AccountCode, string Side, decimal Amount, string Narrative);

public sealed record JournalResponse(
    Guid JournalId,
    string Reference,
    string Description,
    string Currency,
    string Status,
    decimal TotalDebits,
    decimal TotalCredits,
    bool IsBalanced,
    string? SourceType,
    Guid? SourceId,
    DateTimeOffset CreatedAt,
    DateTimeOffset? PostedAt,
    IReadOnlyList<JournalLineResponse> Lines);

public sealed record LedgerAccountResponse(
    Guid LedgerAccountId,
    string Code,
    string Name,
    string Class,
    string Currency,
    Guid? BankAccountId,
    decimal Balance);

public sealed record StatementLineResponse(
    DateTimeOffset PostedAt,
    string Reference,
    string Description,
    string Side,
    decimal Amount,
    decimal BalanceAfter);

public sealed record StatementResponse(
    Guid AccountId,
    string AccountNumber,
    string Currency,
    DateTimeOffset From,
    DateTimeOffset To,
    decimal OpeningBalance,
    decimal ClosingBalance,
    IReadOnlyList<StatementLineResponse> Lines);

public sealed record CustomerAdminResponse(
    Guid CustomerId,
    Guid UserId,
    string Email,
    string FirstName,
    string LastName,
    string Status,
    DateTimeOffset CreatedAt);

public sealed record AuditRecordResponse(
    Guid Id,
    string ActorId,
    string Action,
    string? EntityType,
    string? EntityId,
    DateTimeOffset Timestamp,
    string CorrelationId,
    string? OldValues,
    string? NewValues,
    string? RemoteIp);

public sealed record HealthComponentResponse(string Name, string Status, string? Description);

public sealed record PostJournalRequest(
    string Reference,
    string Description,
    string Currency,
    string IdempotencyKey,
    string? SourceType,
    Guid? SourceId,
    IReadOnlyList<PostJournalLineRequest> Lines);

public sealed record PostJournalLineRequest(Guid LedgerAccountId, string Side, decimal Amount, string Narrative);

public sealed record EnsureCustomerLedgerAccountRequest(
    Guid BankAccountId,
    Guid CustomerId,
    string Currency,
    string DisplayName);
