namespace LedgerX.Eventing.Kafka;

public sealed class KafkaOptions
{
    public const string SectionName = "Kafka";

    public string BootstrapServers { get; set; } = "localhost:9092";

    public string ClientId { get; set; } = "ledgerx";

    public bool Enabled { get; set; } = true;
}
