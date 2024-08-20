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
    /// <param name="settings">The action used to configure the settings.</param>
    /// <returns>A reference to this instance after the operation has completed.</returns>
    public static IServiceCollection AddPostgreSqlAsyncMonolith<T>(
        this IServiceCollection services,
        Action<AsyncMonolithSettings> settings) where T : DbContext =>
        AddPostgreSqlAsyncMonolith<T>(services, settings, AsyncMonolithSettings.Default);

    /// <summary>
    /// Adds the PostgreSql implementation of the AsyncMonolith to the service collection.
    /// </summary>
    /// <typeparam name="T">The type of the DbContext.</typeparam>
    /// <param name="services">The service collection.</param>
    /// <param name="assembly">The assembly containing the DbContext.</param>
    /// <param name="settings">The optional AsyncMonolith settings.</param>
    /// <returns>A reference to this instance after the operation has completed.</returns>
    [Obsolete("This method is obsolete. Use the method that accepts an Action<AsyncMonolithSettings> instead.")]
    public static IServiceCollection AddPostgreSqlAsyncMonolith<T>(
        this IServiceCollection services,
        Assembly assembly,
        AsyncMonolithSettings? settings = null) where T : DbContext =>
        AddPostgreSqlAsyncMonolith<T>(
            services,
            configuration => configuration.RegisterTypesFromAssembly(assembly),
            settings ?? AsyncMonolithSettings.Default);

    private static IServiceCollection AddPostgreSqlAsyncMonolith<T>(
        this IServiceCollection services,
        Action<AsyncMonolithSettings> configuration,
        AsyncMonolithSettings settings) where T : DbContext
    {
        configuration(settings);
        services.InternalAddAsyncMonolith<T>(settings);
        services.AddScoped<IProducerService, PostgreSqlProducerService<T>>();
        services.AddSingleton<IConsumerMessageFetcher, PostgreSqlConsumerMessageFetcher>();
        services.AddSingleton<IScheduledMessageFetcher, PostgreSqlScheduledMessageFetcher>();
        return services;
    }
}
