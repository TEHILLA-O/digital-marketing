using LedgerX.SharedKernel;
using LedgerX.SharedKernel.Money;
using LedgerX.SharedKernel.Primitives;
using LedgerX.SharedKernel.Time;

namespace LedgerX.Ledger.Domain;

/// <summary>
/// Builds balanced journals for the supported money-movement primitives.
/// Customer money is a bank liability. Cash held by the house is an asset.
/// </summary>
public static class JournalFactory
{
    public static Journal Deposit(
        LedgerAccount cash,
        LedgerAccount customerLiability,
        Money amount,
        string reference,
        string idempotencyKey,
        Guid sourceId,
        IClock clock)
    {
        EnsureSystem(cash, ChartOfAccounts.HouseCash);
        EnsureLiability(customerLiability);
        EnsureSameCurrency(cash, customerLiability, amount);

        var journal = Journal.Draft(reference, $"Simulated deposit {amount}", amount.Currency, idempotencyKey, clock, "deposit", sourceId);
        journal.AddLine(cash.Id, EntrySide.Debit, amount, "Increase house cash");
        journal.AddLine(customerLiability.Id, EntrySide.Credit, amount, "Credit customer deposit liability");
        return journal;
    }

    public static Journal Withdrawal(
        LedgerAccount cash,
        LedgerAccount customerLiability,
        Money amount,
        string reference,
        string idempotencyKey,
        Guid sourceId,
        IClock clock)
    {
        EnsureSystem(cash, ChartOfAccounts.HouseCash);
        EnsureLiability(customerLiability);
        EnsureSameCurrency(cash, customerLiability, amount);

        var journal = Journal.Draft(reference, $"Simulated withdrawal {amount}", amount.Currency, idempotencyKey, clock, "withdrawal", sourceId);
        journal.AddLine(customerLiability.Id, EntrySide.Debit, amount, "Reduce customer deposit liability");
        journal.AddLine(cash.Id, EntrySide.Credit, amount, "Decrease house cash");
        return journal;
    }

    public static Journal InternalTransfer(
        LedgerAccount senderLiability,
        LedgerAccount receiverLiability,
        Money amount,
        string reference,
        string idempotencyKey,
        Guid sourceId,
        IClock clock)
    {
        EnsureLiability(senderLiability);
        EnsureLiability(receiverLiability);
        EnsureSameCurrency(senderLiability, receiverLiability, amount);
        Guard.Against(senderLiability.Id == receiverLiability.Id, "Sender and receiver ledger accounts must differ.");

        var journal = Journal.Draft(reference, $"Internal transfer {amount}", amount.Currency, idempotencyKey, clock, "transfer", sourceId);
        journal.AddLine(senderLiability.Id, EntrySide.Debit, amount, "Debit sender customer liability");
        journal.AddLine(receiverLiability.Id, EntrySide.Credit, amount, "Credit receiver customer liability");
        return journal;
    }

    private static void EnsureLiability(LedgerAccount account)
    {
        if (account.Class != AccountClass.Liability)
        {
            throw new DomainException($"Ledger account {account.Code} must be a liability for customer money.", "invalid_ledger_class");
        }
    }

    private static void EnsureSystem(LedgerAccount account, string expectedCode)
    {
        if (!account.IsSystem || !string.Equals(account.Code, expectedCode, StringComparison.Ordinal))
        {
            throw new DomainException($"Expected system account {expectedCode}.", "invalid_system_account");
        }
    }

    private static void EnsureSameCurrency(LedgerAccount left, LedgerAccount right, Money amount)
    {
        if (left.Currency != right.Currency || left.Currency != amount.Currency)
        {
            throw new DomainException("Journal legs and amount must share a currency.", "currency_mismatch");
        }
    }
}
