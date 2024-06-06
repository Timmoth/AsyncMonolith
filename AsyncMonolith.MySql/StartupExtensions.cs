using System.Reflection;
using AsyncMonolith.Consumers;
using AsyncMonolith.Producers;
using AsyncMonolith.Scheduling;
using AsyncMonolith.Utilities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace AsyncMonolith.MySql;

public static class StartupExtensions
{
    public static void AddMySqlAsyncMonolith<T>(this IServiceCollection services, Assembly assembly,
        AsyncMonolithSettings? settings = null) where T : DbContext
    {
        services.InternalAddAsyncMonolith<T>(assembly, settings);
        services.AddScoped<ProducerService<T>, MySqlProducerService<T>>();
        services.AddSingleton<ConsumerMessageFetcher, MySqlConsumerMessageFetcher>();
        services.AddSingleton<ScheduledMessageFetcher, MySqlScheduledMessageFetcher>();
    }
}