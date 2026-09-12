using LedgerX.Eventing.Outbox;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LedgerX.Eventing.Persistence;

public sealed class OutboxMessageConfiguration : IEntityTypeConfiguration<OutboxMessage>
{
    public void Configure(EntityTypeBuilder<OutboxMessage> builder)
    {
        builder.ToTable("outbox_messages");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.EventType).HasMaxLength(128).IsRequired();
        builder.Property(x => x.Topic).HasMaxLength(128).IsRequired();
        builder.Property(x => x.Payload).IsRequired();
        builder.Property(x => x.CorrelationId).HasMaxLength(64).IsRequired();
        builder.Property(x => x.CausationId).HasMaxLength(64);
        builder.Property(x => x.LastError).HasMaxLength(2000);
        builder.HasIndex(x => x.ProcessedAt);
        builder.HasIndex(x => x.OccurredAt);
    }
}

public sealed class InboxMessageConfiguration : IEntityTypeConfiguration<InboxMessage>
{
    public void Configure(EntityTypeBuilder<InboxMessage> builder)
    {
        builder.ToTable("inbox_messages");
        builder.HasKey(x => new { x.EventId, x.ConsumerName });
        builder.Property(x => x.ConsumerName).HasMaxLength(128).IsRequired();
    }
}

public static class OutboxModelBuilderExtensions
{
    public static ModelBuilder AddOutboxAndInbox(this ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfiguration(new OutboxMessageConfiguration());
        modelBuilder.ApplyConfiguration(new InboxMessageConfiguration());
        return modelBuilder;
    }
}
