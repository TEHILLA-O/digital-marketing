using LedgerX.SharedKernel;
using LedgerX.SharedKernel.Money;
using LedgerX.SharedKernel.Primitives;

namespace LedgerX.Ledger.Domain;

public sealed class LedgerLine : Entity<LedgerLineId>
{
    public JournalId JournalId { get; private set; }

    public LedgerAccountId LedgerAccountId { get; private set; }

    public EntrySide Side { get; private set; }

    public Money Amount { get; private set; }

    public string Narrative { get; private set; } = string.Empty;

    private LedgerLine()
    {
    }

    internal static LedgerLine Create(
        JournalId journalId,
        LedgerAccountId ledgerAccountId,
        EntrySide side,
        Money amount,
        string narrative)
    {
        if (!amount.IsPositive)
        {
            throw new DomainException("Ledger line amounts must be strictly positive. Use the opposite side instead of a negative amount.", "invalid_line_amount");
        }

        return new LedgerLine
        {
            Id = LedgerLineId.New(),
            JournalId = journalId,
            LedgerAccountId = ledgerAccountId,
            Side = side,
            Amount = amount,
            Narrative = Guard.AgainstEmpty(narrative, nameof(narrative))
        };
    }
}
