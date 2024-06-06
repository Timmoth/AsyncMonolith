using System.Reflection;
using AsyncMonolith.Consumers;
using AsyncMonolith.Producers;
using AsyncMonolith.Scheduling;
using AsyncMonolith.Utilities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace AsyncMonolith.PostgreSql;

public static class StartupExtensions
{
    public static void AddPostgreSqlAsyncMonolith<T>(this IServiceCollection services, Assembly assembly,
        AsyncMonolithSettings? settings = null) where T : DbContext
    {
        services.InternalAddAsyncMonolith<T>(assembly, settings);
        services.AddScoped<ProducerService<T>, PostgreSqlProducerService<T>>();
        services.AddSingleton<ConsumerMessageFetcher, PostgreSqlConsumerMessageFetcher>();
        services.AddSingleton<ScheduledMessageFetcher, PostgreSqlScheduledMessageFetcher>();
    }
}