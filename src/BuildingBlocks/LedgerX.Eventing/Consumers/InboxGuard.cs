using LedgerX.Eventing.Outbox;

using Microsoft.EntityFrameworkCore;

namespace LedgerX.Eventing.Consumers;

public static class InboxGuard
{
    public static async Task<bool> TryBeginAsync(
        DbContext db,
        Guid eventId,
        string consumerName,
        CancellationToken cancellationToken)
    {
        var exists = await db.Set<InboxMessage>()
            .AnyAsync(x => x.EventId == eventId && x.ConsumerName == consumerName, cancellationToken)
            .ConfigureAwait(false);

        if (exists)
        {
            return false;
        }

        db.Set<InboxMessage>().Add(new InboxMessage
        {
            EventId = eventId,
            ConsumerName = consumerName,
            ProcessedAt = DateTimeOffset.UtcNow
        });

        try
        {
            await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            return true;
        }
        catch (DbUpdateException)
        {
            return false;
        }
    }
}
