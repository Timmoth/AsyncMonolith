﻿using System.Reflection;
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
    /// <param name="settings">The action used to configure the settings.</param>
    /// <returns>A reference to this instance after the operation has completed.</returns>
    public static IServiceCollection AddMySqlAsyncMonolith<T>(
        this IServiceCollection services,
        Action<AsyncMonolithSettings> settings) where T : DbContext =>
        AddMySqlAsyncMonolith<T>(services, settings, AsyncMonolithSettings.Default);

    /// <summary>
    /// Adds MySql implementation of AsyncMonolith to the IServiceCollection.
    /// </summary>
    /// <typeparam name="T">The DbContext type.</typeparam>
    /// <param name="services">The IServiceCollection to add the services to.</param>
    /// <param name="assembly">The assembly containing the DbContext.</param>
    /// <param name="settings">Optional AsyncMonolith settings.</param>
    /// <returns>A reference to this instance after the operation has completed.</returns>
    [Obsolete("This method is obsolete. Use the method that accepts an Action<AsyncMonolithSettings> instead.")]
    public static IServiceCollection AddMySqlAsyncMonolith<T>(this IServiceCollection services, Assembly assembly,
        AsyncMonolithSettings? settings = null) where T : DbContext =>
        AddMySqlAsyncMonolith<T>(
            services,
            configuration => configuration.RegisterTypesFromAssembly(assembly),
            settings ?? AsyncMonolithSettings.Default);

    private static IServiceCollection AddMySqlAsyncMonolith<T>(
        this IServiceCollection services,
        Action<AsyncMonolithSettings> configuration,
        AsyncMonolithSettings settings) where T : DbContext
    {
        configuration(settings);
        services.InternalAddAsyncMonolith<T>(settings);
        services.AddScoped<IProducerService, MySqlProducerService<T>>();
        services.AddSingleton<IConsumerMessageFetcher, MySqlConsumerMessageFetcher>();
        services.AddSingleton<IScheduledMessageFetcher, MySqlScheduledMessageFetcher>();
        return services;
    }
}
