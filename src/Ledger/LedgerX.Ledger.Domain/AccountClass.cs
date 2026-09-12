namespace LedgerX.Ledger.Domain;

/// <summary>
/// Standard chart-of-accounts classification. Customer deposit accounts are liabilities.
/// </summary>
public enum AccountClass
{
    Asset = 1,
    Liability = 2,
    Equity = 3,
    Revenue = 4,
    Expense = 5
}

public enum EntrySide
{
    Debit = 1,
    Credit = 2
}

public enum JournalStatus
{
    Draft = 1,
    Posted = 2,
    Reversed = 3
}

public static class ChartOfAccounts
{
    public const string HouseCash = "ASSET:CASH";
    public const string Clearing = "ASSET:CLEARING";
    public const string FeeRevenue = "REVENUE:FEES";
    public const string CustomerDepositPrefix = "LIAB:CUSTOMER:";

    public static string CustomerDepositCode(Guid bankAccountId) => $"{CustomerDepositPrefix}{bankAccountId:N}";
}
