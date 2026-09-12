namespace LedgerX.SharedKernel.Security;

public static class Roles
{
    public const string Customer = "Customer";
    public const string SupportAgent = "SupportAgent";
    public const string FinanceOperator = "FinanceOperator";
    public const string Administrator = "Administrator";
    public const string Auditor = "Auditor";

    public static readonly IReadOnlyList<string> All =
    [
        Customer,
        SupportAgent,
        FinanceOperator,
        Administrator,
        Auditor
    ];

    public static readonly IReadOnlyList<string> Staff =
    [
        SupportAgent,
        FinanceOperator,
        Administrator,
        Auditor
    ];
}

public static class Policies
{
    public const string CustomerOnly = "CustomerOnly";
    public const string Staff = "Staff";
    public const string Administrator = "Administrator";
    public const string FinanceOperator = "FinanceOperator";
    public const string AuditorReadOnly = "AuditorReadOnly";
    public const string CanFreezeAccounts = "CanFreezeAccounts";
    public const string CanInspectLedger = "CanInspectLedger";
    public const string CanReviewTransfers = "CanReviewTransfers";
    public const string ServiceToService = "ServiceToService";
}

public static class LedgerClaims
{
    public const string CustomerId = "customer_id";
    public const string DisplayName = "display_name";
    public const string CorrelationId = "correlation_id";
}

public static class HeaderNames
{
    public const string IdempotencyKey = "Idempotency-Key";
    public const string CorrelationId = "X-Correlation-Id";
    public const string CausationId = "X-Causation-Id";
    public const string ServiceApiKey = "X-LedgerX-Service-Key";
}
