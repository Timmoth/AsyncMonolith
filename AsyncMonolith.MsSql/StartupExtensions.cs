using System.Reflection;
using AsyncMonolith.Consumers;
using AsyncMonolith.Producers;
using AsyncMonolith.Scheduling;
using AsyncMonolith.Utilities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace AsyncMonolith.MsSql;

/// <summary>
/// AsyncMonolith MsSql startup extensions
/// </summary>
public static class StartupExtensions
{
    /// <summary>
    /// Adds the MsSqlAsyncMonolith services to the IServiceCollection.
    /// </summary>
    /// <typeparam name="T">The type of the DbContext.</typeparam>
    /// <param name="services">The IServiceCollection to add the services to.</param>
    /// <param name="assembly">The assembly containing the DbContext and message handlers.</param>
    /// <param name="settings">The optional AsyncMonolithSettings.</param>
    public static void AddMsSqlAsyncMonolith<T>(this IServiceCollection services, Assembly assembly,
        AsyncMonolithSettings? settings = null) where T : DbContext
    {
        services.InternalAddAsyncMonolith<T>(assembly, settings);
        services.AddScoped<IProducerService, MsSqlProducerService<T>>();
        services.AddSingleton<IConsumerMessageFetcher, MsSqlConsumerMessageFetcher>();
        services.AddSingleton<IScheduledMessageFetcher, MsSqlScheduledMessageFetcher>();
    }
}
