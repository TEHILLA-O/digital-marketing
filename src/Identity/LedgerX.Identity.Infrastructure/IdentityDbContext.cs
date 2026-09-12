using LedgerX.Eventing.Outbox;
using LedgerX.Eventing.Persistence;
using LedgerX.Identity.Domain;
using LedgerX.SharedKernel.Primitives;

using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace LedgerX.Identity.Infrastructure;

public sealed class IdentityDbContext(DbContextOptions<IdentityDbContext> options)
    : IdentityDbContext<ApplicationUser, IdentityRole<Guid>, Guid>(options)
{
    public DbSet<Customer> Customers => Set<Customer>();

    public DbSet<OutboxMessage> OutboxMessages => Set<OutboxMessage>();

    public DbSet<InboxMessage> InboxMessages => Set<InboxMessage>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);
        builder.HasDefaultSchema("identity");
        builder.AddOutboxAndInbox();

        var customerId = new ValueConverter<CustomerId, Guid>(v => v.Value, v => CustomerId.From(v));
        var userId = new ValueConverter<UserId, Guid>(v => v.Value, v => UserId.From(v));

        builder.Entity<Customer>(entity =>
        {
            entity.ToTable("customers");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Id).HasConversion(customerId);
            entity.Property(x => x.UserId).HasConversion(userId);
            entity.Property(x => x.Email).HasMaxLength(256).IsRequired();
            entity.Property(x => x.FirstName).HasMaxLength(100).IsRequired();
            entity.Property(x => x.LastName).HasMaxLength(100).IsRequired();
            entity.Property(x => x.Status).HasConversion<string>().HasMaxLength(32);
            entity.HasIndex(x => x.Email).IsUnique();
            entity.HasIndex(x => x.UserId).IsUnique();
            entity.Ignore(x => x.DomainEvents);
        });

        builder.Entity<ApplicationUser>(entity =>
        {
            entity.Property(x => x.FirstName).HasMaxLength(100);
            entity.Property(x => x.LastName).HasMaxLength(100);
        });
    }
}
