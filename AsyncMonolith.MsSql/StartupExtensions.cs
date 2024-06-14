using System.Reflection;
using AsyncMonolith.Consumers;
using AsyncMonolith.Producers;
using AsyncMonolith.Scheduling;
using AsyncMonolith.Utilities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace AsyncMonolith.MsSql;

public static class StartupExtensions
{
    public static void AddMsSqlAsyncMonolith<T>(this IServiceCollection services, Assembly assembly,
        AsyncMonolithSettings? settings = null) where T : DbContext
    {
        services.InternalAddAsyncMonolith<T>(assembly, settings);
        services.AddScoped<IProducerService, MsSqlProducerService<T>>();
        services.AddSingleton<IConsumerMessageFetcher, MsSqlConsumerMessageFetcher>();
        services.AddSingleton<IScheduledMessageFetcher, MsSqlScheduledMessageFetcher>();
    }
}