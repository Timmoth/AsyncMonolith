using System.Reflection;
using AsyncMonolith.Producers;
using AsyncMonolith.Scheduling;
using AsyncMonolith.Utilities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace AsyncMonolith.TestHelpers;

/// <summary>
/// 
/// </summary>
public static class SetupTestHelpers
{
    /// <summary>
    /// Adds fake implementations of AsyncMonolith base services for testing purposes.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="settings">The action used to configure the settings.</param>
    public static IServiceCollection AddFakeAsyncMonolithBaseServices(
        this IServiceCollection services,
        Action<AsyncMonolithSettings> settings) =>
        AddFakeAsyncMonolithBaseServices(services, settings, AsyncMonolithSettings.Default);

    /// <summary>
    /// Adds fake implementations of AsyncMonolith base services for testing purposes.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="assembly">The assembly containing the consumers.</param>
    /// <param name="settings">Optional AsyncMonolith settings.</param>
    [Obsolete("This method is obsolete. Use the method that accepts an Action<AsyncMonolithSettings> instead.")]
    public static IServiceCollection AddFakeAsyncMonolithBaseServices(
        this IServiceCollection services,
        Assembly assembly,
        AsyncMonolithSettings? settings = null) =>
        AddFakeAsyncMonolithBaseServices(
            services,
            configuration => configuration.RegisterTypesFromAssembly(assembly),
            settings ?? AsyncMonolithSettings.Default);

    /// <summary>
    /// Adds real implementations of AsyncMonolith base services for production use.
    /// </summary>
    /// <typeparam name="T">The DbContext type.</typeparam>
    /// <param name="services">The service collection.</param>
    /// <param name="settings">The action used to configure the settings.</param>
    public static IServiceCollection AddRealAsyncMonolithBaseServices<T>(
        this IServiceCollection services,
        Action<AsyncMonolithSettings> settings) where T : DbContext =>
        AddRealAsyncMonolithBaseServices<T>(services, settings, AsyncMonolithSettings.Default);

    /// <summary>
    /// Adds real implementations of AsyncMonolith base services for production use.
    /// </summary>
    /// <typeparam name="T">The DbContext type.</typeparam>
    /// <param name="services">The service collection.</param>
    /// <param name="assembly">The assembly containing the consumers.</param>
    /// <param name="settings">Optional AsyncMonolith settings.</param>
    [Obsolete("This method is obsolete. Use the method that accepts an Action<AsyncMonolithSettings> instead.")]
    public static IServiceCollection AddRealAsyncMonolithBaseServices<T>(
        this IServiceCollection services,
        Assembly assembly,
        AsyncMonolithSettings? settings = null) where T : DbContext =>
        AddRealAsyncMonolithBaseServices<T>(
            services,
            configuration => configuration.RegisterTypesFromAssembly(assembly),
            settings ?? AsyncMonolithSettings.Default);

    private static IServiceCollection AddFakeAsyncMonolithBaseServices(
        this IServiceCollection services,
        Action<AsyncMonolithSettings> configuration,
        AsyncMonolithSettings settings)
    {
        configuration(settings);
        services.InternalConfigureAsyncMonolithSettings(settings);
        services.InternalRegisterAsyncMonolithConsumers(settings);
        services.AddSingleton<IAsyncMonolithIdGenerator>(new FakeIdGenerator());
        services.AddScoped<IScheduleService, FakeScheduleService>();
        services.AddScoped<IProducerService, FakeProducerService>();
        return services;
    }

    private static IServiceCollection AddRealAsyncMonolithBaseServices<T>(
        this IServiceCollection services,
        Action<AsyncMonolithSettings> configuration,
        AsyncMonolithSettings settings) where T : DbContext
    {
        configuration(settings);
        services.InternalConfigureAsyncMonolithSettings(settings);
        services.InternalRegisterAsyncMonolithConsumers(settings);
        services.AddSingleton<IAsyncMonolithIdGenerator>(new AsyncMonolithIdGenerator());
        services.AddScoped<IScheduleService, ScheduleService<T>>();
        return services;
    }
}
