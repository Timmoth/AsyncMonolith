using System.Reflection;
using AsyncMonolith.Consumers;
using AsyncMonolith.Producers;
using AsyncMonolith.Scheduling;
using AsyncMonolith.Utilities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace AsyncMonolith.MySql;
/// <summary>
/// AsyncMonolith MySql startup extensions
/// </summary>
public static class StartupExtensions
{
    /// <summary>
    /// Adds MySql implementation of AsyncMonolith to the IServiceCollection.
    /// </summary>
    /// <typeparam name="T">The DbContext type.</typeparam>
    /// <param name="services">The IServiceCollection to add the services to.</param>
    /// <param name="assembly">The assembly containing the DbContext.</param>
    /// <param name="settings">Optional AsyncMonolith settings.</param>
    public static void AddMySqlAsyncMonolith<T>(this IServiceCollection services, Assembly assembly,
        AsyncMonolithSettings? settings = null) where T : DbContext
    {
        services.InternalAddAsyncMonolith<T>(assembly, settings);
        services.AddScoped<IProducerService, MySqlProducerService<T>>();
        services.AddSingleton<IConsumerMessageFetcher, MySqlConsumerMessageFetcher>();
        services.AddSingleton<IScheduledMessageFetcher, MySqlScheduledMessageFetcher>();
    }
}
