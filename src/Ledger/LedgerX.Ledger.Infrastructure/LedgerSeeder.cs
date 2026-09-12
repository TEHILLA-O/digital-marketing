using LedgerX.Contracts.Demo;
using LedgerX.Eventing.Persistence;
using LedgerX.Ledger.Domain;
using LedgerX.SharedKernel.Money;
using LedgerX.SharedKernel.Primitives;
using LedgerX.SharedKernel.Time;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace LedgerX.Ledger.Infrastructure;

public sealed class LedgerSeeder(LedgerDbContext db, IClock clock, ILogger<LedgerSeeder> logger)
{
    public async Task SeedAsync(CancellationToken cancellationToken)
    {
        await db.MigrateOrCreateAsync(cancellationToken).ConfigureAwait(false);

        var cash = await EnsureSystem(ChartOfAccounts.HouseCash, "House cash", AccountClass.Asset, cancellationToken).ConfigureAwait(false);
        await EnsureSystem(ChartOfAccounts.Clearing, "Clearing", AccountClass.Asset, cancellationToken).ConfigureAwait(false);
        await EnsureSystem(ChartOfAccounts.FeeRevenue, "Fee revenue", AccountClass.Revenue, cancellationToken).ConfigureAwait(false);

        var aliceCurrent = await EnsureCustomer(DemoIds.AliceCurrent, DemoIds.AliceCustomer, "Alice Current", cancellationToken).ConfigureAwait(false);
        var aliceSavings = await EnsureCustomer(DemoIds.AliceSavings, DemoIds.AliceCustomer, "Alice Savings", cancellationToken).ConfigureAwait(false);
        var bob = await EnsureCustomer(DemoIds.BobCurrent, DemoIds.BobCustomer, "Bob Current", cancellationToken).ConfigureAwait(false);
        var carol = await EnsureCustomer(DemoIds.CarolCurrent, DemoIds.CarolCustomer, "Carol Current", cancellationToken).ConfigureAwait(false);

        await PostIfMissing("DEP-SEED-ALICE-CUR", () => JournalFactory.Deposit(cash, aliceCurrent, Money.Gbp(5000m), "DEP-SEED-ALICE-CUR", "seed:alice-current", DemoIds.AliceCurrent, clock), cancellationToken).ConfigureAwait(false);
        await PostIfMissing("DEP-SEED-ALICE-SAV", () => JournalFactory.Deposit(cash, aliceSavings, Money.Gbp(2500m), "DEP-SEED-ALICE-SAV", "seed:alice-savings", DemoIds.AliceSavings, clock), cancellationToken).ConfigureAwait(false);
        await PostIfMissing("DEP-SEED-BOB", () => JournalFactory.Deposit(cash, bob, Money.Gbp(1000m), "DEP-SEED-BOB", "seed:bob-current", DemoIds.BobCurrent, clock), cancellationToken).ConfigureAwait(false);
        await PostIfMissing("DEP-SEED-CAROL", () => JournalFactory.Deposit(cash, carol, Money.Gbp(750m), "DEP-SEED-CAROL", "seed:carol-current", DemoIds.CarolCurrent, clock), cancellationToken).ConfigureAwait(false);

        logger.LogInformation("Ledger seed completed. Opening balances: Alice Current £5,000, Bob £1,000.");
    }

    private async Task<LedgerAccount> EnsureSystem(string code, string name, AccountClass accountClass, CancellationToken cancellationToken)
    {
        var existing = await db.LedgerAccounts.FirstOrDefaultAsync(a => a.Code == code, cancellationToken).ConfigureAwait(false);
        if (existing is not null)
        {
            return existing;
        }

        var created = LedgerAccount.CreateSystem(code, name, accountClass, Currency.Gbp, clock);
        db.LedgerAccounts.Add(created);
        await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return created;
    }

    private async Task<LedgerAccount> EnsureCustomer(Guid bankAccountId, Guid customerId, string name, CancellationToken cancellationToken)
    {
        var code = ChartOfAccounts.CustomerDepositCode(bankAccountId);
        var existing = await db.LedgerAccounts.FirstOrDefaultAsync(a => a.Code == code, cancellationToken).ConfigureAwait(false);
        if (existing is not null)
        {
            return existing;
        }

        var created = LedgerAccount.CreateCustomerDeposit(BankAccountId.From(bankAccountId), CustomerId.From(customerId), Currency.Gbp, name, clock);
        db.LedgerAccounts.Add(created);
        await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return created;
    }

    private async Task PostIfMissing(string reference, Func<Journal> factory, CancellationToken cancellationToken)
    {
        if (await db.Journals.AnyAsync(j => j.Reference == reference, cancellationToken).ConfigureAwait(false))
        {
            return;
        }

        var journal = factory();
        journal.Post(clock);
        db.Journals.Add(journal);
        db.LedgerLines.AddRange(journal.Lines);
        await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }
}
