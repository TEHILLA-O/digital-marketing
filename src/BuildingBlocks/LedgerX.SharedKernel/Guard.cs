namespace LedgerX.SharedKernel;

public static class Guard
{
    public static T AgainstNull<T>(T? value, string name) where T : class
    {
        return value ?? throw new DomainException($"{name} is required.", "validation");
    }

    public static string AgainstEmpty(string? value, string name)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new DomainException($"{name} is required.", "validation");
        }

        return value.Trim();
    }

    public static void Against(bool condition, string message, string code = "validation")
    {
        if (condition)
        {
            throw new DomainException(message, code);
        }
    }
}
