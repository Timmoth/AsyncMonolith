using AsyncMonolith.Consumers;
using AsyncMonolith.Producers;
using AsyncMonolith.Scheduling;
using AsyncMonolith.Utilities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace AsyncMonolith.Ef;

/// <summary>
/// Extension methods for configuring EF AsyncMonolith in the IServiceCollection.
/// </summary>
/// <summary>
///     Registers AsyncMonolith for any database supported by Entity Framework Core (fetches without row locks).
/// </summary>
public static class StartupExtensions
{
    /// <summary>
    ///     Registers AsyncMonolith: the producer, schedule, poisoned message and statistics services, and the
    ///     background processors. Call it once, in the host.
    /// </summary>
    /// <typeparam name="T">The DbContext containing the AsyncMonolith tables.</typeparam>
    /// <param name="services">The service collection.</param>
    /// <param name="configure">Sets options and the assemblies to scan for consumers.</param>
    /// <returns>A builder to bind configuration or add more consumer assemblies.</returns>
    public static AsyncMonolithBuilder AddEfAsyncMonolith<T>(
        this IServiceCollection services,
        Action<AsyncMonolithOptions>? configure = null) where T : DbContext
    {
        var builder = services.InternalAddAsyncMonolith<T>(configure ?? (_ => { }));
        services.TryAddSingleton<IConsumerMessageFetcher, EfConsumerMessageFetcher>();
        services.TryAddSingleton<IScheduledMessageFetcher, EfScheduledMessageFetcher>();
        return builder;
    }
}
