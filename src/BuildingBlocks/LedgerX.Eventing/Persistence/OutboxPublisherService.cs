using LedgerX.Eventing.Kafka;
using LedgerX.Eventing.Outbox;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace LedgerX.Eventing.Persistence;

/// <summary>
/// Polls unpublished outbox rows and publishes them to Kafka after the originating
/// database transaction has committed. Safe to run as a hosted service per process.
/// </summary>
public sealed class OutboxPublisherService<TContext>(
    IServiceScopeFactory scopeFactory,
    KafkaProducerFactory producer,
    ILogger<OutboxPublisherService<TContext>> logger) : BackgroundService
    where TContext : DbContext
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("Outbox publisher started for {Context}.", typeof(TContext).Name);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await PublishBatchAsync(stoppingToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Outbox publisher iteration failed.");
            }

            await Task.Delay(TimeSpan.FromSeconds(2), stoppingToken).ConfigureAwait(false);
        }
    }

    private async Task PublishBatchAsync(CancellationToken cancellationToken)
    {
        if (!producer.IsEnabled)
        {
            return;
        }

        await using var scope = scopeFactory.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<TContext>();

        var pending = await db.Set<OutboxMessage>()
            .Where(x => x.ProcessedAt == null)
            .OrderBy(x => x.OccurredAt)
            .Take(50)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        foreach (var message in pending)
        {
            try
            {
                await producer.ProduceAsync(message.Topic, message.Id.ToString("N"), message.Payload, cancellationToken)
                    .ConfigureAwait(false);
                message.ProcessedAt = DateTimeOffset.UtcNow;
                message.LastError = null;
            }
            catch (Exception ex)
            {
                message.AttemptCount += 1;
                message.LastError = ex.Message.Length > 2000 ? ex.Message[..2000] : ex.Message;
                logger.LogWarning(ex, "Failed to publish outbox message {Id} ({EventType}).", message.Id, message.EventType);
            }
        }

        if (pending.Count > 0)
        {
            await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }
    }
}
