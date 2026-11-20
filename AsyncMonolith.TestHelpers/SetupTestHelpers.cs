using Microsoft.Extensions.DependencyInjection.Extensions;
using System.Diagnostics.Metrics;
using AsyncMonolith.Consumers;
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
    ///     Registers fake producer and schedule services, which record what is produced and scheduled without a
    ///     database, plus the consumers. No background processors are registered.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configure">Sets options and the assemblies to scan for consumers.</param>
    /// <returns>The service collection.</returns>
    public static IServiceCollection AddFakeAsyncMonolithBaseServices(
        this IServiceCollection services,
        Action<AsyncMonolithOptions> configure)
    {
        AddBaseServices(services, configure);
        services.AddSingleton<IAsyncMonolithIdGenerator>(new FakeIdGenerator());
        services.AddScoped<IScheduleService, FakeScheduleService>();
        services.AddScoped(sp => new FakeProducerService(sp.GetRequiredService<TimeProvider>(),
            sp.GetRequiredService<ConsumerRegistry>(), sp.GetRequiredService<IAsyncMonolithIdGenerator>()));
        services.AddScoped<IProducerService>(sp => sp.GetRequiredService<FakeProducerService>());
        return services;
    }

    /// <summary>
    ///     Registers the real database-backed services and the consumers, but no background processors, so tests can
    ///     process messages themselves with <see cref="TestConsumerMessageProcessor" />.
    /// </summary>
    /// <typeparam name="T">The DbContext containing the AsyncMonolith tables.</typeparam>
    /// <param name="services">The service collection.</param>
    /// <param name="configure">Sets options and the assemblies to scan for consumers.</param>
    /// <returns>The service collection.</returns>
    public static IServiceCollection AddRealAsyncMonolithBaseServices<T>(
        this IServiceCollection services,
        Action<AsyncMonolithOptions> configure) where T : DbContext
    {
        AddBaseServices(services, configure);
        services.AddSingleton<IAsyncMonolithIdGenerator>(new AsyncMonolithIdGenerator());
        services.AddScoped<ProducerService<T>>();
        services.AddScoped<IProducerService, ProducerService<T>>();
        services.AddScoped<IScheduleService, ScheduleService<T>>();
        services.AddScoped<IPoisonedMessageService, PoisonedMessageService<T>>();
        services.AddScoped<IQueueStatisticsService, QueueStatisticsService<T>>();
        return services;
    }

    private static void AddBaseServices(IServiceCollection services, Action<AsyncMonolithOptions> configure)
    {
        services.InternalConfigureAsyncMonolithOptions(configure);
        services.TryAddSingleton(sp => new AsyncMonolithMetrics(sp.GetService<IMeterFactory>()));
        var registration = new AsyncMonolithOptions();
        configure(registration);
        services.InternalRegisterAsyncMonolithConsumers(registration);
    }
}
