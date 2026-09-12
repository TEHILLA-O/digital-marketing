using Confluent.Kafka;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace LedgerX.Eventing.Kafka;

public sealed class KafkaProducerFactory : IDisposable
{
    private readonly IProducer<string, string>? _producer;
    private readonly ILogger<KafkaProducerFactory> _logger;

    public KafkaProducerFactory(IOptions<KafkaOptions> options, ILogger<KafkaProducerFactory> logger)
    {
        _logger = logger;
        var value = options.Value;
        if (!value.Enabled)
        {
            _logger.LogWarning("Kafka producer is disabled. Outbox rows will remain pending until Kafka is enabled.");
            return;
        }

        var config = new ProducerConfig
        {
            BootstrapServers = value.BootstrapServers,
            ClientId = value.ClientId,
            Acks = Acks.All,
            EnableIdempotence = true,
            MessageSendMaxRetries = 5
        };

        _producer = new ProducerBuilder<string, string>(config).Build();
    }

    public bool IsEnabled => _producer is not null;

    public async Task ProduceAsync(string topic, string key, string payload, CancellationToken cancellationToken)
    {
        if (_producer is null)
        {
            throw new InvalidOperationException("Kafka is disabled.");
        }

        var result = await _producer.ProduceAsync(
            topic,
            new Message<string, string> { Key = key, Value = payload },
            cancellationToken).ConfigureAwait(false);

        _logger.LogInformation("Published Kafka message {Topic}/{Partition}@{Offset}", topic, result.Partition.Value, result.Offset.Value);
    }

    public void Dispose() => _producer?.Dispose();
}
