namespace LedgerX.Accounts.Domain;

public enum AccountType
{
    Current = 1,
    Savings = 2
}

public enum AccountStatus
{
    Pending = 1,
    Active = 2,
    Frozen = 3,
    Closed = 4
}
