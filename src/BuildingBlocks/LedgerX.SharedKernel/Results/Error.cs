namespace LedgerX.SharedKernel.Results;

public sealed record Error(string Code, string Message)
{
    public static readonly Error None = new("none", string.Empty);

    public static Error Validation(string message) => new("validation", message);

    public static Error NotFound(string message) => new("not_found", message);

    public static Error Conflict(string message) => new("conflict", message);

    public static Error Forbidden(string message) => new("forbidden", message);

    public static Error Unauthorized(string message) => new("unauthorized", message);

    public static Error Domain(string code, string message) => new(code, message);
}
