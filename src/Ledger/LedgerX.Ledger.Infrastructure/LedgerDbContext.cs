using LedgerX.Eventing.Outbox;
using LedgerX.Eventing.Persistence;
using LedgerX.Ledger.Domain;
using LedgerX.SharedKernel.Money;
using LedgerX.SharedKernel.Primitives;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace LedgerX.Ledger.Infrastructure;

public sealed class LedgerDbContext(DbContextOptions<LedgerDbContext> options) : DbContext(options)
{
    public DbSet<LedgerAccount> LedgerAccounts => Set<LedgerAccount>();

    public DbSet<Journal> Journals => Set<Journal>();

    public DbSet<LedgerLine> LedgerLines => Set<LedgerLine>();

    public DbSet<OutboxMessage> OutboxMessages => Set<OutboxMessage>();

    public DbSet<InboxMessage> InboxMessages => Set<InboxMessage>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema("ledger");
        modelBuilder.AddOutboxAndInbox();

        var ledgerAccountId = new ValueConverter<LedgerAccountId, Guid>(v => v.Value, v => LedgerAccountId.From(v));
        var journalId = new ValueConverter<JournalId, Guid>(v => v.Value, v => JournalId.From(v));
        var lineId = new ValueConverter<LedgerLineId, Guid>(v => v.Value, v => LedgerLineId.From(v));
        var bankAccountId = new ValueConverter<BankAccountId, Guid>(v => v.Value, v => BankAccountId.From(v));
        var customerId = new ValueConverter<CustomerId, Guid>(v => v.Value, v => CustomerId.From(v));
        var currency = new ValueConverter<Currency, string>(v => v.Code, v => Currency.Parse(v));

        modelBuilder.Entity<LedgerAccount>(entity =>
        {
            entity.ToTable("ledger_accounts");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Id).HasConversion(ledgerAccountId);
            entity.Property(x => x.Code).HasMaxLength(80).IsRequired();
            entity.Property(x => x.Name).HasMaxLength(200).IsRequired();
            entity.Property(x => x.Class).HasConversion<string>().HasMaxLength(32);
            entity.Property(x => x.Currency).HasConversion(currency).HasMaxLength(3);
            entity.Property(x => x.BankAccountId).HasConversion(bankAccountId);
            entity.Property(x => x.CustomerId).HasConversion(customerId);
            entity.HasIndex(x => x.Code).IsUnique();
            entity.HasIndex(x => x.BankAccountId);
            entity.Ignore(x => x.DomainEvents);
        });

        modelBuilder.Entity<Journal>(entity =>
        {
            entity.ToTable("journals");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Id).HasConversion(journalId);
            entity.Property(x => x.Reference).HasMaxLength(64).IsRequired();
            entity.Property(x => x.Description).HasMaxLength(500).IsRequired();
            entity.Property(x => x.Currency).HasConversion(currency).HasMaxLength(3);
            entity.Property(x => x.Status).HasConversion<string>().HasMaxLength(32);
            entity.Property(x => x.IdempotencyKey).HasMaxLength(128).IsRequired();
            entity.Property(x => x.SourceType).HasMaxLength(64);
            entity.Property(x => x.ReversesJournalId).HasConversion(journalId);
            entity.Property(x => x.ReversedByJournalId).HasConversion(journalId);
            entity.HasIndex(x => x.Reference).IsUnique();
            entity.HasIndex(x => x.IdempotencyKey).IsUnique();
            entity.HasIndex(x => x.PostedAt);
            entity.Ignore(x => x.DomainEvents);
            entity.Ignore(x => x.Lines);
            entity.Ignore(x => x.TotalDebits);
            entity.Ignore(x => x.TotalCredits);
            entity.Ignore(x => x.IsBalanced);
        });

        modelBuilder.Entity<LedgerLine>(entity =>
        {
            entity.ToTable("ledger_lines");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Id).HasConversion(lineId);
            entity.Property(x => x.JournalId).HasConversion(journalId);
            entity.Property(x => x.LedgerAccountId).HasConversion(ledgerAccountId);
            entity.Property(x => x.Side).HasConversion<string>().HasMaxLength(16);
            entity.Property(x => x.Narrative).HasMaxLength(300).IsRequired();
            entity.ComplexProperty(x => x.Amount, money =>
            {
                money.Property(m => m.Amount).HasColumnName("amount").HasPrecision(18, 2);
                money.Property(m => m.Currency).HasColumnName("currency").HasMaxLength(3).HasConversion(currency);
            });
            entity.HasIndex(x => x.JournalId);
            entity.HasIndex(x => x.LedgerAccountId);
            entity.ToTable(t => t.HasCheckConstraint("ck_ledger_lines_amount_positive", "amount > 0"));
        });
    }
}
