namespace LedgerX.SharedKernel.Primitives;

public readonly record struct CustomerId(Guid Value)
{
    public static CustomerId New() => new(Guid.NewGuid());
    public static CustomerId From(Guid value) => new(value);
    public override string ToString() => Value.ToString();
}

public readonly record struct UserId(Guid Value)
{
    public static UserId New() => new(Guid.NewGuid());
    public static UserId From(Guid value) => new(value);
    public override string ToString() => Value.ToString();
}

public readonly record struct BankAccountId(Guid Value)
{
    public static BankAccountId New() => new(Guid.NewGuid());
    public static BankAccountId From(Guid value) => new(value);
    public override string ToString() => Value.ToString();
}

public readonly record struct BeneficiaryId(Guid Value)
{
    public static BeneficiaryId New() => new(Guid.NewGuid());
    public static BeneficiaryId From(Guid value) => new(value);
    public override string ToString() => Value.ToString();
}

public readonly record struct TransferId(Guid Value)
{
    public static TransferId New() => new(Guid.NewGuid());
    public static TransferId From(Guid value) => new(value);
    public override string ToString() => Value.ToString();
}

public readonly record struct PaymentId(Guid Value)
{
    public static PaymentId New() => new(Guid.NewGuid());
    public static PaymentId From(Guid value) => new(value);
    public override string ToString() => Value.ToString();
}

public readonly record struct JournalId(Guid Value)
{
    public static JournalId New() => new(Guid.NewGuid());
    public static JournalId From(Guid value) => new(value);
    public override string ToString() => Value.ToString();
}

public readonly record struct LedgerAccountId(Guid Value)
{
    public static LedgerAccountId New() => new(Guid.NewGuid());
    public static LedgerAccountId From(Guid value) => new(value);
    public override string ToString() => Value.ToString();
}

public readonly record struct LedgerLineId(Guid Value)
{
    public static LedgerLineId New() => new(Guid.NewGuid());
    public static LedgerLineId From(Guid value) => new(value);
    public override string ToString() => Value.ToString();
}

public readonly record struct AuditRecordId(Guid Value)
{
    public static AuditRecordId New() => new(Guid.NewGuid());
    public static AuditRecordId From(Guid value) => new(value);
    public override string ToString() => Value.ToString();
}

public readonly record struct OutboxMessageId(Guid Value)
{
    public static OutboxMessageId New() => new(Guid.NewGuid());
    public static OutboxMessageId From(Guid value) => new(value);
    public override string ToString() => Value.ToString();
}
