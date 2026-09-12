using LedgerX.Eventing.Outbox;
using LedgerX.Eventing.Persistence;

using Microsoft.EntityFrameworkCore;

namespace LedgerX.Audit.Worker;

public sealed class AuditDbContext(DbContextOptions<AuditDbContext> options) : DbContext(options)
{
    public DbSet<AuditRecord> AuditRecords => Set<AuditRecord>();

    public DbSet<InboxMessage> InboxMessages => Set<InboxMessage>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema("audit");
        modelBuilder.ApplyConfiguration(new InboxMessageConfiguration());
        modelBuilder.Entity<AuditRecord>(entity =>
        {
            entity.ToTable("audit_records");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.ActorId).HasMaxLength(64).IsRequired();
            entity.Property(x => x.Action).HasMaxLength(64).IsRequired();
            entity.Property(x => x.EntityType).HasMaxLength(64);
            entity.Property(x => x.EntityId).HasMaxLength(64);
            entity.Property(x => x.CorrelationId).HasMaxLength(64).IsRequired();
            entity.HasIndex(x => x.Timestamp);
            entity.HasIndex(x => x.Action);
            entity.HasIndex(x => x.EntityId);
        });
    }
}
