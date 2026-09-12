namespace LedgerX.Payments.Domain;

public enum TransferStatus
{
    Created = 1,
    Validated = 2,
    Processing = 3,
    Completed = 4,
    Rejected = 5,
    Failed = 6,
    Cancelled = 7
}

public enum PaymentKind
{
    InternalTransfer = 1,
    Deposit = 2,
    Withdrawal = 3
}
