using FluentAssertions;

using LedgerX.Ledger.Domain;
using LedgerX.SharedKernel;
using LedgerX.SharedKernel.Money;
using LedgerX.SharedKernel.Primitives;
using LedgerX.SharedKernel.Time;

namespace LedgerX.Domain.Tests;

public sealed class JournalTests
{
    private readonly FixedClock _clock = new(new DateTimeOffset(2026, 9, 12, 12, 0, 0, TimeSpan.Zero));

    [Fact]
    public void Unbalanced_journal_is_rejected()
    {
        var journal = Journal.Draft("J1", "unbalanced", Currency.Gbp, "k1", _clock);
        journal.AddLine(LedgerAccountId.New(), EntrySide.Debit, Money.Gbp(100m), "dr");
        journal.AddLine(LedgerAccountId.New(), EntrySide.Credit, Money.Gbp(80m), "cr");
        var act = () => journal.Post(_clock);
        act.Should().Throw<DomainException>().Which.Code.Should().Be("unbalanced_journal");
    }

    [Fact]
    public void Balanced_journal_is_accepted()
    {
        var journal = Journal.Draft("J2", "balanced", Currency.Gbp, "k2", _clock);
        journal.AddLine(LedgerAccountId.New(), EntrySide.Debit, Money.Gbp(100m), "dr");
        journal.AddLine(LedgerAccountId.New(), EntrySide.Credit, Money.Gbp(100m), "cr");
        journal.Post(_clock);
        journal.Status.Should().Be(JournalStatus.Posted);
        journal.IsBalanced.Should().BeTrue();
        journal.TotalDebits.Should().Be(journal.TotalCredits);
    }

    [Fact]
    public void Posted_lines_cannot_be_edited()
    {
        var journal = Journal.Draft("J3", "posted", Currency.Gbp, "k3", _clock);
        journal.AddLine(LedgerAccountId.New(), EntrySide.Debit, Money.Gbp(10m), "dr");
        journal.AddLine(LedgerAccountId.New(), EntrySide.Credit, Money.Gbp(10m), "cr");
        journal.Post(_clock);
        var act = () => journal.AddLine(LedgerAccountId.New(), EntrySide.Debit, Money.Gbp(1m), "nope");
        act.Should().Throw<DomainException>().Which.Code.Should().Be("journal_immutable");
    }

    [Fact]
    public void Reversal_restores_opposite_sides()
    {
        var a = LedgerAccountId.New();
        var b = LedgerAccountId.New();
        var journal = Journal.Draft("J4", "xfer", Currency.Gbp, "k4", _clock);
        journal.AddLine(a, EntrySide.Debit, Money.Gbp(50m), "sender");
        journal.AddLine(b, EntrySide.Credit, Money.Gbp(50m), "receiver");
        journal.Post(_clock);

        var reversal = journal.Reverse("REV-J4", _clock);
        reversal.IsBalanced.Should().BeTrue();
        reversal.Lines.Should().Contain(l => l.LedgerAccountId == a && l.Side == EntrySide.Credit);
        reversal.Lines.Should().Contain(l => l.LedgerAccountId == b && l.Side == EntrySide.Debit);
        journal.Status.Should().Be(JournalStatus.Reversed);
    }

    [Fact]
    public void Internal_transfer_preserves_accounting_equality()
    {
        var clock = _clock;
        var sender = LedgerAccount.CreateCustomerDeposit(BankAccountId.New(), CustomerId.New(), Currency.Gbp, "Alice", clock);
        var receiver = LedgerAccount.CreateCustomerDeposit(BankAccountId.New(), CustomerId.New(), Currency.Gbp, "Bob", clock);
        var journal = JournalFactory.InternalTransfer(sender, receiver, Money.Gbp(500m), "TRF-1", "idem-1", Guid.NewGuid(), clock);
        journal.Post(clock);
        journal.TotalDebits.Should().Be(journal.TotalCredits);
        journal.TotalDebits.Should().Be(Money.Gbp(500m));
    }
}
