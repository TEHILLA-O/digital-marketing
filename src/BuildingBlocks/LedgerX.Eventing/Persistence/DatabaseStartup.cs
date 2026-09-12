using Microsoft.EntityFrameworkCore;

namespace LedgerX.Eventing.Persistence;

public static class DatabaseStartup
{
    public static async Task MigrateOrCreateAsync(this DbContext db, CancellationToken cancellationToken = default)
    {
        var pending = await db.Database.GetPendingMigrationsAsync(cancellationToken).ConfigureAwait(false);
        var applied = await db.Database.GetAppliedMigrationsAsync(cancellationToken).ConfigureAwait(false);
        if (pending.Any() || applied.Any())
        {
            await db.Database.MigrateAsync(cancellationToken).ConfigureAwait(false);
            return;
        }

        await db.Database.EnsureCreatedAsync(cancellationToken).ConfigureAwait(false);
    }
}
