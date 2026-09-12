using FluentAssertions;

using NetArchTest.Rules;

namespace LedgerX.ArchitectureTests;

public sealed class LayeringTests
{
    [Fact]
    public void Domain_does_not_depend_on_infrastructure()
    {
        var result = Types.InCurrentDomain()
            .That()
            .ResideInNamespace("LedgerX.Ledger.Domain")
            .Or()
            .ResideInNamespace("LedgerX.Accounts.Domain")
            .Or()
            .ResideInNamespace("LedgerX.Payments.Domain")
            .Or()
            .ResideInNamespace("LedgerX.Identity.Domain")
            .ShouldNot()
            .HaveDependencyOnAny(
                "LedgerX.Ledger.Infrastructure",
                "LedgerX.Accounts.Infrastructure",
                "LedgerX.Payments.Infrastructure",
                "LedgerX.Identity.Infrastructure",
                "Microsoft.EntityFrameworkCore")
            .GetResult();

        result.IsSuccessful.Should().BeTrue();
    }

    [Fact]
    public void Application_does_not_depend_on_ef_core()
    {
        var result = Types.InCurrentDomain()
            .That()
            .ResideInNamespace("LedgerX.Ledger.Application")
            .Or()
            .ResideInNamespace("LedgerX.Payments.Application")
            .Or()
            .ResideInNamespace("LedgerX.Accounts.Application")
            .ShouldNot()
            .HaveDependencyOn("Microsoft.EntityFrameworkCore")
            .GetResult();

        result.IsSuccessful.Should().BeTrue();
    }
}
