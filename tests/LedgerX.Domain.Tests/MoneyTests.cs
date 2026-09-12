using FluentAssertions;

using LedgerX.SharedKernel;
using LedgerX.SharedKernel.Money;

namespace LedgerX.Domain.Tests;

public sealed class MoneyTests
{
    [Fact]
    public void Adds_same_currency()
    {
        (Money.Gbp(10.50m) + Money.Gbp(2.25m)).Should().Be(Money.Gbp(12.75m));
    }

    [Fact]
    public void Rejects_mixed_currency_arithmetic()
    {
        var act = () => Money.Gbp(10m) + Money.Usd(10m);
        act.Should().Throw<CurrencyMismatchException>();
    }

    [Fact]
    public void Rejects_excess_scale()
    {
        var act = () => Money.Gbp(1.234m);
        act.Should().Throw<DomainException>();
    }

    [Fact]
    public void Comparison_requires_same_currency()
    {
        (Money.Gbp(5m) > Money.Gbp(2m)).Should().BeTrue();
        var act = () => _ = Money.Gbp(5m) > Money.Eur(2m);
        act.Should().Throw<CurrencyMismatchException>();
    }
}
