using System.Reflection;
using AsyncMonolith.Consumers;
using AsyncMonolith.Producers;
using AsyncMonolith.Scheduling;
using AsyncMonolith.Utilities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace AsyncMonolith.PostgreSql;

/// <summary>
/// AsyncMonolith PostgreSql startup extensions
/// </summary>
public static class StartupExtensions
{
    /// <summary>
    /// Adds the PostgreSql implementation of the AsyncMonolith to the service collection.
    /// </summary>
    /// <typeparam name="T">The type of the DbContext.</typeparam>
    /// <param name="services">The service collection.</param>
    /// <param name="assembly">The assembly containing the DbContext.</param>
    /// <param name="settings">The optional AsyncMonolith settings.</param>
    public static void AddPostgreSqlAsyncMonolith<T>(this IServiceCollection services, Assembly assembly,
        AsyncMonolithSettings? settings = null) where T : DbContext
    {
        services.InternalAddAsyncMonolith<T>(assembly, settings);
        services.AddScoped<IProducerService, PostgreSqlProducerService<T>>();
        services.AddSingleton<IConsumerMessageFetcher, PostgreSqlConsumerMessageFetcher>();
        services.AddSingleton<IScheduledMessageFetcher, PostgreSqlScheduledMessageFetcher>();
    }
}
