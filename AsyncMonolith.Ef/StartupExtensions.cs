using System.Reflection;
using AsyncMonolith.Consumers;
using AsyncMonolith.Producers;
using AsyncMonolith.Scheduling;
using AsyncMonolith.Utilities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace AsyncMonolith.Ef;

/// <summary>
/// Extension methods for configuring EF AsyncMonolith in the IServiceCollection.
/// </summary>
public static class StartupExtensions
{
    /// <summary>
    /// Adds EF AsyncMonolith to the IServiceCollection.
    /// </summary>
    /// <typeparam name="T">The DbContext type.</typeparam>
    /// <param name="services">The IServiceCollection to add the services to.</param>
    /// <param name="settings">The action used to configure the settings.</param>
    /// <returns>A reference to this instance after the operation has completed.</returns>
    /// <exception cref="ArgumentException">Thrown when the ConsumerMessageProcessorCount or ScheduledMessageProcessorCount is greater than 1.</exception>
    public static IServiceCollection AddEfAsyncMonolith<T>(
        this IServiceCollection services,
        Action<AsyncMonolithSettings> settings) where T : DbContext =>
        AddEfAsyncMonolith<T>(services, settings, AsyncMonolithSettings.Default);

    /// <summary>
    /// Adds EF AsyncMonolith to the IServiceCollection.
    /// </summary>
    /// <typeparam name="T">The DbContext type.</typeparam>
    /// <param name="services">The IServiceCollection to add the services to.</param>
    /// <param name="assembly">The assembly containing the DbContext.</param>
    /// <param name="settings">The optional AsyncMonolithSettings.</param>
    /// <returns>A reference to this instance after the operation has completed.</returns>
    /// <exception cref="ArgumentException">Thrown when the ConsumerMessageProcessorCount or ScheduledMessageProcessorCount is greater than 1.</exception>
    [Obsolete("This method is obsolete. Use the method that accepts an Action<AsyncMonolithSettings> instead.")]
    public static IServiceCollection AddEfAsyncMonolith<T>(
        this IServiceCollection services,
        Assembly assembly,
        AsyncMonolithSettings? settings = null) where T : DbContext =>
        AddEfAsyncMonolith<T>(
            services,
            configuration => configuration.RegisterTypesFromAssembly(assembly),
            settings ?? AsyncMonolithSettings.Default);

    private static IServiceCollection AddEfAsyncMonolith<T>(
        this IServiceCollection services,
        Action<AsyncMonolithSettings> configuration,
        AsyncMonolithSettings settings) where T : DbContext
    {
        configuration(settings);

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

        services.InternalAddAsyncMonolith<T>(settings);
        services.AddScoped<IProducerService, EfProducerService<T>>();
        services.AddSingleton<IConsumerMessageFetcher, EfConsumerMessageFetcher>();
        services.AddSingleton<IScheduledMessageFetcher, EfScheduledMessageFetcher>();
        return services;
    }
}
