using Confluent.Kafka;

using LedgerX.Eventing.Kafka;

using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace LedgerX.Eventing.Consumers;

public abstract class KafkaConsumerService(
    IOptions<KafkaOptions> options,
    ILogger logger,
    string groupId,
    IReadOnlyList<string> topics) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!options.Value.Enabled)
        {
            logger.LogWarning("Kafka consumer {Group} is disabled.", groupId);
            return;
        }

        var config = new ConsumerConfig
        {
            BootstrapServers = options.Value.BootstrapServers,
            GroupId = groupId,
            ClientId = $"{options.Value.ClientId}-{groupId}",
            AutoOffsetReset = AutoOffsetReset.Earliest,
            EnableAutoCommit = false
        };

        using var consumer = new ConsumerBuilder<string, string>(config).Build();
        consumer.Subscribe(topics);
        logger.LogInformation("Kafka consumer {Group} subscribed to {Topics}.", groupId, string.Join(",", topics));

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var result = consumer.Consume(TimeSpan.FromSeconds(1));
                if (result is null)
                {
                    continue;
                }

                await HandleAsync(result.Message.Value, result.Topic, stoppingToken).ConfigureAwait(false);
                consumer.Commit(result);
            }
            catch (ConsumeException ex)
            {
                logger.LogWarning(ex, "Kafka consume error in {Group}.", groupId);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Kafka handler failed in {Group}.", groupId);
            }
        }

        consumer.Close();
    }

    protected abstract Task HandleAsync(string payload, string topic, CancellationToken cancellationToken);
}
