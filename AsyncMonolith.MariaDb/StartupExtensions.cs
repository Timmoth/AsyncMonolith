using System.Reflection;
using AsyncMonolith.Consumers;
using AsyncMonolith.Producers;
using AsyncMonolith.Scheduling;
using AsyncMonolith.Utilities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace AsyncMonolith.MariaDb;
/// <summary>
/// AsyncMonolith MariaDb startup extensions
/// </summary>
public static class StartupExtensions
{
    /// <summary>
    /// Adds MariaDb implementation of AsyncMonolith to the IServiceCollection.
    /// </summary>
    /// <typeparam name="T">The DbContext type.</typeparam>
    /// <param name="services">The IServiceCollection to add the services to.</param>
    /// <param name="assembly">The assembly containing the DbContext.</param>
    /// <param name="settings">Optional AsyncMonolith settings.</param>
    public static void AddMariaDbAsyncMonolith<T>(this IServiceCollection services, Assembly assembly,
        AsyncMonolithSettings? settings = null) where T : DbContext
    {
        services.InternalAddAsyncMonolith<T>(assembly, settings);
        services.AddScoped<IProducerService, MariaDbProducerService<T>>();
        services.AddSingleton<IConsumerMessageFetcher, MariaDbConsumerMessageFetcher>();
        services.AddSingleton<IScheduledMessageFetcher, MariaDbScheduledMessageFetcher>();
    }
}
