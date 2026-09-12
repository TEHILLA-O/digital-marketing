using LedgerX.Eventing.Kafka;
using LedgerX.Eventing.Outbox;
using LedgerX.Eventing.Persistence;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace LedgerX.Eventing;

public static class DependencyInjection
{
    public static IServiceCollection AddLedgerXEventing<TContext>(this IServiceCollection services, IConfiguration configuration)
        where TContext : DbContext
    {
        services.Configure<KafkaOptions>(configuration.GetSection(KafkaOptions.SectionName));
        services.AddSingleton<KafkaProducerFactory>();
        services.AddScoped<IOutboxWriter, EfOutboxWriter<TContext>>();
        services.AddHostedService<OutboxPublisherService<TContext>>();
        return services;
    }
}
