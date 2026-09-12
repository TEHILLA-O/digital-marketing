using IsoCurrency = LedgerX.SharedKernel.Money.Currency;

namespace LedgerX.SharedKernel.Money;

/// <summary>
/// Monetary amount with a currency. Never use <see cref="double"/> or <see cref="float"/> for money.
/// Arithmetic rejects mixed-currency operations.
/// </summary>
public readonly record struct Money : IComparable<Money>
{
    public const int Scale = 2;

    public decimal Amount { get; }
    public Currency Currency { get; }

    public Money(decimal amount, Currency currency)
    {
        if (decimal.Round(amount, Scale, MidpointRounding.AwayFromZero) != amount)
        {
            throw new DomainException($"Money amounts must have at most {Scale} decimal places. Received {amount}.");
        }

        Amount = amount;
        Currency = currency;
    }

    public static Money Of(decimal amount, string currency) => new(amount, IsoCurrency.Parse(currency));

    public static Money Zero(Currency currency) => new(0m, currency);

    public static Money Gbp(decimal amount) => new(amount, IsoCurrency.Gbp);

    public static Money Usd(decimal amount) => new(amount, IsoCurrency.Usd);

    public static Money Eur(decimal amount) => new(amount, IsoCurrency.Eur);

    public bool IsZero => Amount == 0m;

    public bool IsPositive => Amount > 0m;

    public bool IsNegative => Amount < 0m;

    public Money Abs() => new(Math.Abs(Amount), Currency);

    public Money Negate() => new(-Amount, Currency);

    public static Money operator +(Money left, Money right)
    {
        EnsureSameCurrency(left, right);
        return new Money(left.Amount + right.Amount, left.Currency);
    }

    public static Money operator -(Money left, Money right)
    {
        EnsureSameCurrency(left, right);
        return new Money(left.Amount - right.Amount, left.Currency);
    }

    public static Money operator *(Money left, decimal factor)
    {
        var raw = decimal.Round(left.Amount * factor, Scale, MidpointRounding.AwayFromZero);
        return new Money(raw, left.Currency);
    }

    public static bool operator >(Money left, Money right)
    {
        EnsureSameCurrency(left, right);
        return left.Amount > right.Amount;
    }

    public static bool operator <(Money left, Money right)
    {
        EnsureSameCurrency(left, right);
        return left.Amount < right.Amount;
    }

    public static bool operator >=(Money left, Money right)
    {
        EnsureSameCurrency(left, right);
        return left.Amount >= right.Amount;
    }

    public static bool operator <=(Money left, Money right)
    {
        EnsureSameCurrency(left, right);
        return left.Amount <= right.Amount;
    }

    public int CompareTo(Money other)
    {
        EnsureSameCurrency(this, other);
        return Amount.CompareTo(other.Amount);
    }

    public static void EnsureSameCurrency(Money left, Money right)
    {
        if (left.Currency != right.Currency)
        {
            throw new CurrencyMismatchException(left.Currency, right.Currency);
        }
    }

    public override string ToString() => $"{Currency.Code} {Amount:N2}";
}
