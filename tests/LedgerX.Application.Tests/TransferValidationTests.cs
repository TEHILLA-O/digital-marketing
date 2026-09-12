using FluentAssertions;

using LedgerX.Payments.Domain;
using LedgerX.SharedKernel;

namespace LedgerX.Application.Tests;

public sealed class TransferValidationTests
{
    [Theory]
    [InlineData(TransferStatus.Completed, TransferStatus.Processing)]
    [InlineData(TransferStatus.Rejected, TransferStatus.Validated)]
    [InlineData(TransferStatus.Failed, TransferStatus.Completed)]
    [InlineData(TransferStatus.Cancelled, TransferStatus.Created)]
    public void Illegal_transitions_are_rejected(TransferStatus from, TransferStatus to)
    {
        var act = () => TransferStateMachine.Ensure(from, to);
        act.Should().Throw<IllegalStateTransitionException>();
    }

    [Fact]
    public void Terminal_states_are_identified()
    {
        TransferStateMachine.IsTerminal(TransferStatus.Completed).Should().BeTrue();
        TransferStateMachine.IsTerminal(TransferStatus.Created).Should().BeFalse();
    }
}
