using LedgerX.SharedKernel;
using LedgerX.SharedKernel.Money;
using LedgerX.SharedKernel.Primitives;
using LedgerX.SharedKernel.Time;

namespace LedgerX.Ledger.Domain;

public sealed class LedgerAccount : AggregateRoot<LedgerAccountId>
{
    public string Code { get; private set; } = string.Empty;

    public string Name { get; private set; } = string.Empty;

    public AccountClass Class { get; private set; }

    public Currency Currency { get; private set; }

    public BankAccountId? BankAccountId { get; private set; }

    public CustomerId? CustomerId { get; private set; }

    public bool IsSystem { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    private LedgerAccount()
    {
    }

    public static LedgerAccount CreateSystem(
        string code,
        string name,
        AccountClass accountClass,
        Currency currency,
        IClock clock)
    {
        return new LedgerAccount
        {
            Id = LedgerAccountId.New(),
            Code = Guard.AgainstEmpty(code, nameof(code)),
            Name = Guard.AgainstEmpty(name, nameof(name)),
            Class = accountClass,
            Currency = currency,
            IsSystem = true,
            CreatedAt = clock.UtcNow
        };
    }

    public static LedgerAccount CreateCustomerDeposit(
        BankAccountId bankAccountId,
        CustomerId customerId,
        Currency currency,
        string displayName,
        IClock clock)
    {
        return new LedgerAccount
        {
            Id = LedgerAccountId.New(),
            Code = ChartOfAccounts.CustomerDepositCode(bankAccountId.Value),
            Name = $"Customer deposits — {displayName}",
            Class = AccountClass.Liability,
            Currency = currency,
            BankAccountId = bankAccountId,
            CustomerId = customerId,
            IsSystem = false,
            CreatedAt = clock.UtcNow
        };
    }

    /// <summary>
    /// Debits increase assets and expenses; credits increase liabilities, equity and revenue.
    /// </summary>
    public decimal SignedImpact(EntrySide side, decimal amount) =>
        Class switch
        {
            AccountClass.Asset or AccountClass.Expense => side == EntrySide.Debit ? amount : -amount,
            _ => side == EntrySide.Credit ? amount : -amount
        };
}
