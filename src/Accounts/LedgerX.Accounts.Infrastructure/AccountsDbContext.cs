using LedgerX.Accounts.Domain;
using LedgerX.Eventing.Outbox;
using LedgerX.Eventing.Persistence;
using LedgerX.SharedKernel.Money;
using LedgerX.SharedKernel.Primitives;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace LedgerX.Accounts.Infrastructure;

public sealed class AccountsDbContext(DbContextOptions<AccountsDbContext> options) : DbContext(options)
{
    public DbSet<BankAccount> Accounts => Set<BankAccount>();

    public DbSet<Beneficiary> Beneficiaries => Set<Beneficiary>();

    public DbSet<OutboxMessage> OutboxMessages => Set<OutboxMessage>();

    public DbSet<InboxMessage> InboxMessages => Set<InboxMessage>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema("accounts");
        modelBuilder.AddOutboxAndInbox();

        var accountId = new ValueConverter<BankAccountId, Guid>(v => v.Value, v => BankAccountId.From(v));
        var customerId = new ValueConverter<CustomerId, Guid>(v => v.Value, v => CustomerId.From(v));
        var beneficiaryId = new ValueConverter<BeneficiaryId, Guid>(v => v.Value, v => BeneficiaryId.From(v));
        var currency = new ValueConverter<Currency, string>(v => v.Code, v => Currency.Parse(v));

        modelBuilder.Entity<BankAccount>(entity =>
        {
            entity.ToTable("bank_accounts");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Id).HasConversion(accountId);
            entity.Property(x => x.CustomerId).HasConversion(customerId);
            entity.Property(x => x.Currency).HasConversion(currency).HasMaxLength(3);
            entity.Property(x => x.AccountType).HasConversion<string>().HasMaxLength(32);
            entity.Property(x => x.Status).HasConversion<string>().HasMaxLength(32);
            entity.Property(x => x.DisplayName).HasMaxLength(120).IsRequired();
            entity.Property(x => x.ProjectedBalance).HasPrecision(18, 2);
            entity.Property(x => x.Version)
                .HasColumnName("xmin")
                .HasColumnType("xid")
                .ValueGeneratedOnAddOrUpdate()
                .IsConcurrencyToken();
            entity.OwnsOne(x => x.AccountNumber, n =>
            {
                n.Property(p => p.Value).HasColumnName("account_number").HasMaxLength(8).IsRequired();
                n.Property(p => p.SortCode).HasColumnName("sort_code").HasMaxLength(16).IsRequired();
                n.HasIndex(p => p.Value).IsUnique();
            });
            entity.HasIndex(x => x.CustomerId);
            entity.HasIndex(x => x.Status);
            entity.Ignore(x => x.DomainEvents);
        });

        modelBuilder.Entity<Beneficiary>(entity =>
        {
            entity.ToTable("beneficiaries");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Id).HasConversion(beneficiaryId);
            entity.Property(x => x.OwnerCustomerId).HasConversion(customerId);
            entity.Property(x => x.LinkedBankAccountId).HasConversion(accountId);
            entity.Property(x => x.Currency).HasConversion(currency).HasMaxLength(3);
            entity.Property(x => x.DisplayName).HasMaxLength(120).IsRequired();
            entity.Property(x => x.AccountNumber).HasMaxLength(8).IsRequired();
            entity.Property(x => x.SortCode).HasMaxLength(16).IsRequired();
            entity.HasIndex(x => new { x.OwnerCustomerId, x.AccountNumber, x.SortCode }).IsUnique();
            entity.Ignore(x => x.DomainEvents);
        });
    }
}
