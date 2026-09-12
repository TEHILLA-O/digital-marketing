namespace LedgerX.SharedKernel.Context;

public sealed class CorrelationContext
{
    public string CorrelationId { get; init; } = Guid.NewGuid().ToString("N");

    public string? CausationId { get; init; }

    public string? ActorId { get; init; }

    public string? ActorRoles { get; init; }

    public string? RemoteIp { get; init; }

    public string? UserAgent { get; init; }
}

public interface ICorrelationAccessor
{
    CorrelationContext Current { get; set; }
}

public sealed class CorrelationAccessor : ICorrelationAccessor
{
    private static readonly AsyncLocal<CorrelationContext?> Holder = new();

    public CorrelationContext Current
    {
        get => Holder.Value ?? new CorrelationContext();
        set => Holder.Value = value;
    }
}
