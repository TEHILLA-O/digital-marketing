using LedgerX.SharedKernel;
using LedgerX.SharedKernel.Primitives;

namespace LedgerX.Payments.Domain;

public sealed class IdempotencyRecord : Entity<Guid>
{
    public string OwnerKey { get; private set; } = string.Empty;

    public string IdempotencyKey { get; private set; } = string.Empty;

    public string RequestHash { get; private set; } = string.Empty;

    public int StatusCode { get; private set; }

    public string ResponseBody { get; private set; } = string.Empty;

    public DateTimeOffset CreatedAt { get; private set; }

    public DateTimeOffset ExpiresAt { get; private set; }

    private IdempotencyRecord()
    {
    }

    public static IdempotencyRecord Capture(
        string ownerKey,
        string idempotencyKey,
        string requestHash,
        int statusCode,
        string responseBody,
        DateTimeOffset createdAt,
        TimeSpan lifetime)
    {
        return new IdempotencyRecord
        {
            Id = Guid.NewGuid(),
            OwnerKey = Guard.AgainstEmpty(ownerKey, nameof(ownerKey)),
            IdempotencyKey = Guard.AgainstEmpty(idempotencyKey, nameof(idempotencyKey)),
            RequestHash = Guard.AgainstEmpty(requestHash, nameof(requestHash)),
            StatusCode = statusCode,
            ResponseBody = responseBody,
            CreatedAt = createdAt,
            ExpiresAt = createdAt.Add(lifetime)
        };
    }

    public void EnsureSameRequest(string requestHash)
    {
        if (!string.Equals(RequestHash, requestHash, StringComparison.Ordinal))
        {
            throw new ConflictException(
                "Idempotency-Key was reused with a different request payload.",
                "idempotency_payload_mismatch");
        }
    }
}
