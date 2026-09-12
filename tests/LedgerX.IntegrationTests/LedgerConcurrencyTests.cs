using FluentAssertions;

using LedgerX.Eventing.Outbox;
using LedgerX.Eventing.Persistence;
using LedgerX.Ledger.Domain;
using LedgerX.Ledger.Infrastructure;
using LedgerX.SharedKernel;
using LedgerX.SharedKernel.Context;
using LedgerX.SharedKernel.Money;
using LedgerX.SharedKernel.Primitives;
using LedgerX.SharedKernel.Time;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

using Testcontainers.PostgreSql;

namespace LedgerX.IntegrationTests;

public sealed class LedgerConcurrencyTests : IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder()
        .WithImage("postgres:16-alpine")
        .WithDatabase("ledgerx_ledger")
        .WithUsername("ledgerx")
        .WithPassword("ledgerx")
        .Build();

    public Task InitializeAsync() => _postgres.StartAsync();

    public Task DisposeAsync() => _postgres.DisposeAsync().AsTask();

    [Fact]
    public async Task Duplicate_idempotency_does_not_double_post()
    {
        await using var db = CreateDb();
        await db.Database.EnsureCreatedAsync();
        var (cash, alice, bob) = await SeedAsync(db);
        var service = CreateService(db);

        var first = await service.PostTransferAsync(alice.BankAccountId!.Value.Value, bob.BankAccountId!.Value.Value, 40m, "GBP", "TRF-DUP-1", "same-key", Guid.NewGuid(), CancellationToken.None);
        var second = await service.PostTransferAsync(alice.BankAccountId!.Value.Value, bob.BankAccountId!.Value.Value, 40m, "GBP", "TRF-DUP-1", "same-key", Guid.NewGuid(), CancellationToken.None);

        first.JournalId.Should().Be(second.JournalId);
        (await service.GetCustomerBalanceAsync(alice.BankAccountId!.Value.Value, CancellationToken.None)).Should().Be(60m);
    }

    [Fact]
    public async Task Concurrent_overspend_cannot_both_succeed()
    {
        await using var db = CreateDb();
        await db.Database.EnsureCreatedAsync();
        var (_, alice, bob) = await SeedAsync(db);

        async Task<bool> Attempt(string key)
        {
            await using var scoped = CreateDb();
            var service = CreateService(scoped);
            try
            {
                await service.PostTransferAsync(alice.BankAccountId!.Value.Value, bob.BankAccountId!.Value.Value, 80m, "GBP", $"TRF-{key}", key, Guid.NewGuid(), CancellationToken.None);
                return true;
            }
            catch (InsufficientFundsException)
            {
                return false;
            }
        }

        var results = await Task.WhenAll(Attempt("a"), Attempt("b"));
        results.Count(x => x).Should().Be(1);

        await using var check = CreateDb();
        var remaining = await CreateService(check).GetCustomerBalanceAsync(alice.BankAccountId!.Value.Value, CancellationToken.None);
        remaining.Should().Be(20m);
    }

    private LedgerDbContext CreateDb()
    {
        var options = new DbContextOptionsBuilder<LedgerDbContext>()
            .UseNpgsql(_postgres.GetConnectionString())
            .UseSnakeCaseNamingConvention()
            .Options;
        return new LedgerDbContext(options);
    }

    private static LedgerService CreateService(LedgerDbContext db) =>
        new(db, new EfOutboxWriter<LedgerDbContext>(db), new SystemClock(), new CorrelationAccessor());

    private static async Task<(LedgerAccount Cash, LedgerAccount Alice, LedgerAccount Bob)> SeedAsync(LedgerDbContext db)
    {
        var clock = new SystemClock();
        var cash = LedgerAccount.CreateSystem(ChartOfAccounts.HouseCash, "Cash", AccountClass.Asset, Currency.Gbp, clock);
        var alice = LedgerAccount.CreateCustomerDeposit(BankAccountId.New(), CustomerId.New(), Currency.Gbp, "Alice", clock);
        var bob = LedgerAccount.CreateCustomerDeposit(BankAccountId.New(), CustomerId.New(), Currency.Gbp, "Bob", clock);
        db.LedgerAccounts.AddRange(cash, alice, bob);
        var opening = JournalFactory.Deposit(cash, alice, Money.Gbp(100m), "DEP-ALICE", "seed-alice", Guid.NewGuid(), clock);
        opening.Post(clock);
        db.Journals.Add(opening);
        db.LedgerLines.AddRange(opening.Lines);
        await db.SaveChangesAsync();
        return (cash, alice, bob);
    }
}
