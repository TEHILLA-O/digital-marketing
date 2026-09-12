namespace LedgerX.SharedKernel.Money;

/// <summary>
/// ISO-4217 currency supported by the LedgerX simulation.
/// Only GBP, USD and EUR are enabled in this educational system.
/// </summary>
public readonly record struct Currency
{
    public static readonly Currency Gbp = new("GBP");
    public static readonly Currency Usd = new("USD");
    public static readonly Currency Eur = new("EUR");

    public static readonly IReadOnlyList<Currency> Supported = [Gbp, Usd, Eur];

    public string Code { get; }

    private Currency(string code) => Code = code;

    public static Currency Default => Gbp;

    public static Currency Parse(string? code)
    {
        if (string.IsNullOrWhiteSpace(code))
        {
            throw new DomainException("Currency code is required.");
        }

        var normalised = code.Trim().ToUpperInvariant();
        return normalised switch
        {
            "GBP" => Gbp,
            "USD" => Usd,
            "EUR" => Eur,
            _ => throw new DomainException($"Unsupported currency '{code}'. LedgerX currently supports GBP, USD and EUR.")
        };
    }

    public static bool TryParse(string? code, out Currency currency)
    {
        try
        {
            currency = Parse(code);
            return true;
        }
        catch (DomainException)
        {
            currency = default;
            return false;
        }
    }

    public override string ToString() => Code;
}
