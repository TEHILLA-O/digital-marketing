using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

using LedgerX.Payments.Domain;
using LedgerX.SharedKernel;
using LedgerX.SharedKernel.Time;

using Microsoft.EntityFrameworkCore;

using StackExchange.Redis;

namespace LedgerX.Payments.Infrastructure;

public sealed class RedisIdempotencyStore(IConnectionMultiplexer redis, PaymentsDbContext db, IClock clock)
{
    private static readonly TimeSpan Ttl = TimeSpan.FromHours(24);

    public static string HashRequest(object payload)
    {
        var json = JsonSerializer.Serialize(payload);
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(json)));
    }

    public async Task<T?> TryGetAsync<T>(string ownerKey, string idempotencyKey, string requestHash, CancellationToken cancellationToken)
    {
        var cache = redis.GetDatabase();
        var cached = await cache.StringGetAsync(CacheKey(ownerKey, idempotencyKey)).ConfigureAwait(false);
        if (cached.HasValue)
        {
            return JsonSerializer.Deserialize<T>(cached.ToString());
        }

        var record = await db.IdempotencyRecords
            .FirstOrDefaultAsync(x => x.OwnerKey == ownerKey && x.IdempotencyKey == idempotencyKey, cancellationToken)
            .ConfigureAwait(false);

        if (record is null)
        {
            return default;
        }

        record.EnsureSameRequest(requestHash);
        return JsonSerializer.Deserialize<T>(record.ResponseBody);
    }

    public async Task SaveAsync<T>(string ownerKey, string idempotencyKey, string requestHash, T response, CancellationToken cancellationToken)
    {
        var json = JsonSerializer.Serialize(response);
        var record = IdempotencyRecord.Capture(ownerKey, idempotencyKey, requestHash, 200, json, clock.UtcNow, Ttl);
        db.IdempotencyRecords.Add(record);
        try
        {
            await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (DbUpdateException)
        {
            var existing = await db.IdempotencyRecords
                .FirstOrDefaultAsync(x => x.OwnerKey == ownerKey && x.IdempotencyKey == idempotencyKey, cancellationToken)
                .ConfigureAwait(false);
            existing?.EnsureSameRequest(requestHash);
        }

        await redis.GetDatabase().StringSetAsync(CacheKey(ownerKey, idempotencyKey), json, Ttl).ConfigureAwait(false);
    }

    private static string CacheKey(string owner, string key) => $"ledgerx:idempotency:{owner}:{key}";
}

public sealed class RedisLock
{
    public static async Task<T> ExecuteAsync<T>(IConnectionMultiplexer redis, string name, TimeSpan expiry, Func<Task<T>> action)
    {
        var db = redis.GetDatabase();
        var token = Guid.NewGuid().ToString("N");
        var acquired = await db.StringSetAsync($"ledgerx:lock:{name}", token, expiry, When.NotExists).ConfigureAwait(false);
        if (!acquired)
        {
            throw new ConcurrencyConflictException("Another operation is already in progress for this account. Retry shortly.");
        }

        try
        {
            return await action().ConfigureAwait(false);
        }
        finally
        {
            var stored = await db.StringGetAsync($"ledgerx:lock:{name}").ConfigureAwait(false);
            if (stored == token)
            {
                await db.KeyDeleteAsync($"ledgerx:lock:{name}").ConfigureAwait(false);
            }
        }
    }
}
