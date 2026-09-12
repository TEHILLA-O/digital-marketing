namespace LedgerX.Contracts.Demo;

/// <summary>
/// Deterministic identifiers so every service can seed a coherent demo world.
/// </summary>
public static class DemoIds
{
    public static readonly Guid AliceUser = Guid.Parse("aaaaaaaa-0001-0001-0001-000000000001");
    public static readonly Guid AliceCustomer = Guid.Parse("cccccccc-0001-0001-0001-000000000001");
    public static readonly Guid AliceCurrent = Guid.Parse("bbbbbbbb-0001-0001-0001-000000000001");
    public static readonly Guid AliceSavings = Guid.Parse("bbbbbbbb-0001-0001-0001-000000000002");

    public static readonly Guid BobUser = Guid.Parse("aaaaaaaa-0001-0001-0001-000000000002");
    public static readonly Guid BobCustomer = Guid.Parse("cccccccc-0001-0001-0001-000000000002");
    public static readonly Guid BobCurrent = Guid.Parse("bbbbbbbb-0001-0001-0001-000000000003");

    public static readonly Guid CarolUser = Guid.Parse("aaaaaaaa-0001-0001-0001-000000000003");
    public static readonly Guid CarolCustomer = Guid.Parse("cccccccc-0001-0001-0001-000000000003");
    public static readonly Guid CarolCurrent = Guid.Parse("bbbbbbbb-0001-0001-0001-000000000004");

    public static readonly Guid AdminUser = Guid.Parse("aaaaaaaa-0001-0001-0001-000000000010");
    public static readonly Guid AuditorUser = Guid.Parse("aaaaaaaa-0001-0001-0001-000000000011");
    public static readonly Guid FinanceUser = Guid.Parse("aaaaaaaa-0001-0001-0001-000000000012");
    public static readonly Guid SupportUser = Guid.Parse("aaaaaaaa-0001-0001-0001-000000000013");

    public static readonly Guid DemoTransferAliceToBob = Guid.Parse("dddddddd-0001-0001-0001-000000000001");
    public static readonly Guid DemoJournalAliceToBob = Guid.Parse("eeeeeeee-0001-0001-0001-000000000001");
}
