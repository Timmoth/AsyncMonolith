using System.Reflection;
using AsyncMonolith.Consumers;
using AsyncMonolith.Producers;
using AsyncMonolith.Scheduling;
using AsyncMonolith.Utilities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace AsyncMonolith.Ef;

public static class StartupExtensions
{
    public static void AddEfAsyncMonolith<T>(this IServiceCollection services, Assembly assembly,
        AsyncMonolithSettings? settings = null) where T : DbContext
    {
        settings ??= AsyncMonolithSettings.Default;

        if (settings.ConsumerMessageProcessorCount > 1)
        {
            throw new ArgumentException(
                "AsyncMonolithSettings.ConsumerMessageProcessorCount can only be set to 1 when using 'DbType.Ef'.");
        }

        if (settings.ScheduledMessageProcessorCount > 1)
        {
            throw new ArgumentException(
                "AsyncMonolithSettings.ScheduledMessageProcessorCount can only be set to 1 when using 'DbType.Ef'.");
        }

        services.InternalAddAsyncMonolith<T>(assembly, settings);
        services.AddScoped<IProducerService, EfProducerService<T>>();
        services.AddSingleton<IConsumerMessageFetcher, EfConsumerMessageFetcher>();
        services.AddSingleton<IScheduledMessageFetcher, EfScheduledMessageFetcher>();
    }
}