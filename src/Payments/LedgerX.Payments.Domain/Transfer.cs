using LedgerX.SharedKernel;
using LedgerX.SharedKernel.Money;
using LedgerX.SharedKernel.Primitives;
using LedgerX.SharedKernel.Time;

namespace LedgerX.Payments.Domain;

public sealed class Transfer : AggregateRoot<TransferId>
{
    public string Reference { get; private set; } = string.Empty;

    public PaymentKind Kind { get; private set; }

    public CustomerId SenderCustomerId { get; private set; }

    public BankAccountId SourceAccountId { get; private set; }

    public BankAccountId? DestinationAccountId { get; private set; }

    public BeneficiaryId? BeneficiaryId { get; private set; }

    public Money Amount { get; private set; }

    public string Description { get; private set; } = string.Empty;

    public TransferStatus Status { get; private set; }

    public string IdempotencyKey { get; private set; } = string.Empty;

    public string? FailureReason { get; private set; }

    public JournalId? JournalId { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public DateTimeOffset? CompletedAt { get; private set; }

    public DateTimeOffset UpdatedAt { get; private set; }

    public uint Version { get; private set; }

    private Transfer()
    {
    }

    public static Transfer Create(
        PaymentKind kind,
        CustomerId senderCustomerId,
        BankAccountId sourceAccountId,
        BankAccountId? destinationAccountId,
        BeneficiaryId? beneficiaryId,
        Money amount,
        string description,
        string idempotencyKey,
        IClock clock)
    {
        if (!amount.IsPositive)
        {
            throw new DomainException("Payment amount must be greater than zero.", "invalid_amount");
        }

        if (kind == PaymentKind.InternalTransfer)
        {
            Guard.Against(destinationAccountId is null, "Internal transfers require a destination account.");
            Guard.Against(sourceAccountId == destinationAccountId, "Source and destination accounts must differ.");
        }

        var now = clock.UtcNow;
        var id = TransferId.New();
        return new Transfer
        {
            Id = id,
            Reference = CreateReference(kind, now, id),
            Kind = kind,
            SenderCustomerId = senderCustomerId,
            SourceAccountId = sourceAccountId,
            DestinationAccountId = destinationAccountId,
            BeneficiaryId = beneficiaryId,
            Amount = amount,
            Description = string.IsNullOrWhiteSpace(description) ? kind.ToString() : description.Trim(),
            Status = TransferStatus.Created,
            IdempotencyKey = Guard.AgainstEmpty(idempotencyKey, nameof(idempotencyKey)),
            CreatedAt = now,
            UpdatedAt = now
        };
    }

    public void MarkValidated(IClock clock) => Transition(TransferStatus.Validated, clock);

    public void MarkProcessing(IClock clock) => Transition(TransferStatus.Processing, clock);

    public void MarkCompleted(JournalId journalId, IClock clock)
    {
        Transition(TransferStatus.Completed, clock);
        JournalId = journalId;
        CompletedAt = clock.UtcNow;
    }

    public void Reject(string reason, IClock clock)
    {
        Transition(TransferStatus.Rejected, clock);
        FailureReason = Guard.AgainstEmpty(reason, nameof(reason));
    }

    public void Fail(string reason, IClock clock)
    {
        Transition(TransferStatus.Failed, clock);
        FailureReason = Guard.AgainstEmpty(reason, nameof(reason));
    }

    public void Cancel(string reason, IClock clock)
    {
        Transition(TransferStatus.Cancelled, clock);
        FailureReason = Guard.AgainstEmpty(reason, nameof(reason));
    }

    private void Transition(TransferStatus next, IClock clock)
    {
        TransferStateMachine.Ensure(Status, next);
        Status = next;
        UpdatedAt = clock.UtcNow;
    }

    private static string CreateReference(PaymentKind kind, DateTimeOffset now, TransferId id)
    {
        var prefix = kind switch
        {
            PaymentKind.Deposit => "DEP",
            PaymentKind.Withdrawal => "WTH",
            _ => "TRF"
        };

        return $"{prefix}-{now:yyyyMMdd}-{id.Value.ToString("N")[..8].ToUpperInvariant()}";
    }
}
