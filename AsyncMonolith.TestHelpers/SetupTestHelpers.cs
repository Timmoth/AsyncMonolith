using System.Reflection;
using AsyncMonolith.Producers;
using AsyncMonolith.Scheduling;
using AsyncMonolith.Utilities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace AsyncMonolith.TestHelpers;

public static class SetupTestHelpers
{
    /// <summary>
    /// Adds fake implementations of AsyncMonolith base services for testing purposes.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="assembly">The assembly containing the consumers.</param>
    /// <param name="settings">Optional AsyncMonolith settings.</param>
    public static void AddFakeAsyncMonolithBaseServices(
        this IServiceCollection services,
        Assembly assembly,
        AsyncMonolithSettings? settings = null)
    {
        settings = services.InternalConfigureAsyncMonolithSettings(settings);
        services.InternalRegisterAsyncMonolithConsumers(assembly, settings);
        services.AddSingleton<IAsyncMonolithIdGenerator>(new FakeIdGenerator());
        services.AddScoped<IScheduleService, FakeScheduleService>();
        services.AddScoped<IProducerService, FakeProducerService>();
    }

    /// <summary>
    /// Adds real implementations of AsyncMonolith base services for production use.
    /// </summary>
    /// <typeparam name="T">The DbContext type.</typeparam>
    /// <param name="services">The service collection.</param>
    /// <param name="assembly">The assembly containing the consumers.</param>
    /// <param name="settings">Optional AsyncMonolith settings.</param>
    public static void AddRealAsyncMonolithBaseServices<T>(
        this IServiceCollection services,
        Assembly assembly,
        AsyncMonolithSettings? settings = null) where T : DbContext
    {
        settings = services.InternalConfigureAsyncMonolithSettings(settings);
        services.InternalRegisterAsyncMonolithConsumers(assembly, settings);
        services.AddSingleton<IAsyncMonolithIdGenerator>(new AsyncMonolithIdGenerator());
        services.AddScoped<IScheduleService, ScheduleService<T>>();
    }
}
