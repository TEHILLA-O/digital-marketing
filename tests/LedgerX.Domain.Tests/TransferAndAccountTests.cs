using FluentAssertions;

using LedgerX.Accounts.Domain;
using LedgerX.Payments.Domain;
using LedgerX.SharedKernel;
using LedgerX.SharedKernel.Money;
using LedgerX.SharedKernel.Primitives;
using LedgerX.SharedKernel.Time;

namespace LedgerX.Domain.Tests;

public sealed class TransferAndAccountTests
{
    private readonly FixedClock _clock = new(DateTimeOffset.UtcNow);

    [Fact]
    public void Completed_transfer_cannot_return_to_processing()
    {
        TransferStateMachine.CanTransition(TransferStatus.Completed, TransferStatus.Processing).Should().BeFalse();
        var act = () => TransferStateMachine.Ensure(TransferStatus.Completed, TransferStatus.Processing);
        act.Should().Throw<IllegalStateTransitionException>();
    }

    [Fact]
    public void Happy_path_transitions_are_legal()
    {
        TransferStateMachine.CanTransition(TransferStatus.Created, TransferStatus.Validated).Should().BeTrue();
        TransferStateMachine.CanTransition(TransferStatus.Validated, TransferStatus.Processing).Should().BeTrue();
        TransferStateMachine.CanTransition(TransferStatus.Processing, TransferStatus.Completed).Should().BeTrue();
    }

    [Fact]
    public void Frozen_account_cannot_send()
    {
        var account = BankAccount.Open(CustomerId.New(), AccountType.Current, Currency.Gbp, "Frozen", _clock);
        account.Freeze(_clock);
        var act = () => account.EnsureCanSend(Money.Gbp(10m));
        act.Should().Throw<DomainException>();
        account.CanReceiveMoney.Should().BeTrue();
    }

    [Fact]
    public void Closed_account_cannot_receive()
    {
        var account = BankAccount.Open(CustomerId.New(), AccountType.Current, Currency.Gbp, "Closed", _clock);
        account.Close(_clock);
        var act = () => account.EnsureCanReceive(Money.Gbp(10m));
        act.Should().Throw<DomainException>();
    }

    [Fact]
    public void Transfer_amount_must_be_positive()
    {
        var act = () => Transfer.Create(
            PaymentKind.InternalTransfer,
            CustomerId.New(),
            BankAccountId.New(),
            BankAccountId.New(),
            null,
            Money.Gbp(0m),
            "x",
            "key",
            _clock);
        act.Should().Throw<DomainException>();
    }
}
