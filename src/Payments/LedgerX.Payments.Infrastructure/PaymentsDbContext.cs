using LedgerX.Eventing.Outbox;
using LedgerX.Eventing.Persistence;
using LedgerX.Payments.Domain;
using LedgerX.SharedKernel.Money;
using LedgerX.SharedKernel.Primitives;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace LedgerX.Payments.Infrastructure;

public sealed class PaymentsDbContext(DbContextOptions<PaymentsDbContext> options) : DbContext(options)
{
    public DbSet<Transfer> Transfers => Set<Transfer>();

    public DbSet<IdempotencyRecord> IdempotencyRecords => Set<IdempotencyRecord>();

    public DbSet<OutboxMessage> OutboxMessages => Set<OutboxMessage>();

    public DbSet<InboxMessage> InboxMessages => Set<InboxMessage>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema("payments");
        modelBuilder.AddOutboxAndInbox();

        var transferId = new ValueConverter<TransferId, Guid>(v => v.Value, v => TransferId.From(v));
        var customerId = new ValueConverter<CustomerId, Guid>(v => v.Value, v => CustomerId.From(v));
        var accountId = new ValueConverter<BankAccountId, Guid>(v => v.Value, v => BankAccountId.From(v));
        var beneficiaryId = new ValueConverter<BeneficiaryId, Guid>(v => v.Value, v => BeneficiaryId.From(v));
        var journalId = new ValueConverter<JournalId, Guid>(v => v.Value, v => JournalId.From(v));
        var currency = new ValueConverter<Currency, string>(v => v.Code, v => Currency.Parse(v));

        modelBuilder.Entity<Transfer>(entity =>
        {
            entity.ToTable("transfers");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Id).HasConversion(transferId);
            entity.Property(x => x.Reference).HasMaxLength(40).IsRequired();
            entity.Property(x => x.Kind).HasConversion<string>().HasMaxLength(32);
            entity.Property(x => x.SenderCustomerId).HasConversion(customerId);
            entity.Property(x => x.SourceAccountId).HasConversion(accountId);
            entity.Property(x => x.DestinationAccountId).HasConversion(accountId);
            entity.Property(x => x.BeneficiaryId).HasConversion(beneficiaryId);
            entity.Property(x => x.JournalId).HasConversion(journalId);
            entity.Property(x => x.Status).HasConversion<string>().HasMaxLength(32);
            entity.Property(x => x.Description).HasMaxLength(280);
            entity.Property(x => x.FailureReason).HasMaxLength(500);
            entity.Property(x => x.IdempotencyKey).HasMaxLength(128).IsRequired();
            entity.ComplexProperty(x => x.Amount, money =>
            {
                money.Property(m => m.Amount).HasColumnName("amount").HasPrecision(18, 2);
                money.Property(m => m.Currency).HasColumnName("currency").HasMaxLength(3).HasConversion(currency);
            });
            entity.Property(x => x.Version)
                .HasColumnName("xmin")
                .HasColumnType("xid")
                .ValueGeneratedOnAddOrUpdate()
                .IsConcurrencyToken();
            entity.HasIndex(x => x.Reference).IsUnique();
            entity.HasIndex(x => new { x.SenderCustomerId, x.IdempotencyKey }).IsUnique();
            entity.HasIndex(x => x.Status);
            entity.HasIndex(x => x.CreatedAt);
            entity.Ignore(x => x.DomainEvents);
        });

        modelBuilder.Entity<IdempotencyRecord>(entity =>
        {
            entity.ToTable("idempotency_records");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.OwnerKey).HasMaxLength(128).IsRequired();
            entity.Property(x => x.IdempotencyKey).HasMaxLength(128).IsRequired();
            entity.Property(x => x.RequestHash).HasMaxLength(128).IsRequired();
            entity.Property(x => x.ResponseBody).IsRequired();
            entity.HasIndex(x => new { x.OwnerKey, x.IdempotencyKey }).IsUnique();
        });
    }
}
