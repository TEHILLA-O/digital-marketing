using LedgerX.SharedKernel;
using LedgerX.SharedKernel.Money;
using LedgerX.SharedKernel.Primitives;
using LedgerX.SharedKernel.Time;

namespace LedgerX.Ledger.Domain;

/// <summary>
/// Immutable posted journal. SUM(debits) must equal SUM(credits) or posting is rejected.
/// Corrections are reversals — lines are never edited or deleted after posting.
/// </summary>
public sealed class Journal : AggregateRoot<JournalId>
{
    private readonly List<LedgerLine> _lines = [];

    public string Reference { get; private set; } = string.Empty;

    public string Description { get; private set; } = string.Empty;

    public Currency Currency { get; private set; }

    public JournalStatus Status { get; private set; }

    public string IdempotencyKey { get; private set; } = string.Empty;

    public string? SourceType { get; private set; }

    public Guid? SourceId { get; private set; }

    public JournalId? ReversesJournalId { get; private set; }

    public JournalId? ReversedByJournalId { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public DateTimeOffset? PostedAt { get; private set; }

    public IReadOnlyList<LedgerLine> Lines => _lines.AsReadOnly();

    public Money TotalDebits => Sum(EntrySide.Debit);

    public Money TotalCredits => Sum(EntrySide.Credit);

    public bool IsBalanced => _lines.Count >= 2 && TotalDebits == TotalCredits;

    private Journal()
    {
    }

    public static Journal Draft(
        string reference,
        string description,
        Currency currency,
        string idempotencyKey,
        IClock clock,
        string? sourceType = null,
        Guid? sourceId = null,
        JournalId? reverses = null,
        JournalId? id = null)
    {
        return new Journal
        {
            Id = id ?? JournalId.New(),
            Reference = Guard.AgainstEmpty(reference, nameof(reference)),
            Description = Guard.AgainstEmpty(description, nameof(description)),
            Currency = currency,
            Status = JournalStatus.Draft,
            IdempotencyKey = Guard.AgainstEmpty(idempotencyKey, nameof(idempotencyKey)),
            SourceType = sourceType,
            SourceId = sourceId,
            ReversesJournalId = reverses,
            CreatedAt = clock.UtcNow
        };
    }

    public void AddLine(LedgerAccountId accountId, EntrySide side, Money amount, string narrative)
    {
        EnsureDraft();
        if (amount.Currency != Currency)
        {
            throw new CurrencyMismatchException(amount.Currency, Currency);
        }

        _lines.Add(LedgerLine.Create(Id, accountId, side, amount, narrative));
    }

    public void Post(IClock clock)
    {
        EnsureDraft();

        if (_lines.Count < 2)
        {
            throw new DomainException("A journal must contain at least two lines before it can be posted.", "unbalanced_journal");
        }

        if (!IsBalanced)
        {
            throw new DomainException(
                $"Journal {Reference} is not balanced. Debits {TotalDebits} != Credits {TotalCredits}.",
                "unbalanced_journal");
        }

        Status = JournalStatus.Posted;
        PostedAt = clock.UtcNow;
    }

    public Journal Reverse(string reversalReference, IClock clock)
    {
        if (Status != JournalStatus.Posted)
        {
            throw new DomainException("Only posted journals can be reversed.", "journal_not_posted");
        }

        if (ReversedByJournalId is not null)
        {
            throw new DomainException($"Journal {Reference} has already been reversed.", "already_reversed");
        }

        var reversal = Draft(
            reversalReference,
            $"Reversal of {Reference}",
            Currency,
            $"rev:{IdempotencyKey}",
            clock,
            sourceType: "reversal",
            sourceId: Id.Value,
            reverses: Id);

        foreach (var line in _lines)
        {
            var opposite = line.Side == EntrySide.Debit ? EntrySide.Credit : EntrySide.Debit;
            reversal.AddLine(line.LedgerAccountId, opposite, line.Amount, $"Reversal: {line.Narrative}");
        }

        reversal.Post(clock);
        ReversedByJournalId = reversal.Id;
        Status = JournalStatus.Reversed;
        return reversal;
    }

    public void AttachExistingLine(LedgerLine line)
    {
        _lines.Add(line);
    }

    private Money Sum(EntrySide side)
    {
        var total = 0m;
        foreach (var line in _lines)
        {
            if (line.Side == side)
            {
                total += line.Amount.Amount;
            }
        }

        return new Money(total, Currency);
    }

    private void EnsureDraft()
    {
        if (Status != JournalStatus.Draft)
        {
            throw new DomainException("Posted ledger lines are immutable. Create a reversal journal to correct them.", "journal_immutable");
        }
    }
}
