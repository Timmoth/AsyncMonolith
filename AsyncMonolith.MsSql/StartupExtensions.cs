using AsyncMonolith.Consumers;
using AsyncMonolith.Producers;
using AsyncMonolith.Scheduling;
using AsyncMonolith.Utilities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace AsyncMonolith.MsSql;

/// <summary>
/// AsyncMonolith MsSql startup extensions
/// </summary>
/// <summary>
///     Registers AsyncMonolith for SQL Server.
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
    public static AsyncMonolithBuilder AddMsSqlAsyncMonolith<T>(
        this IServiceCollection services,
        Action<AsyncMonolithOptions>? configure = null) where T : DbContext
    {
        var builder = services.InternalAddAsyncMonolith<T>(configure ?? (_ => { }));
        services.TryAddSingleton<IConsumerMessageFetcher, MsSqlConsumerMessageFetcher>();
        services.TryAddSingleton<IScheduledMessageFetcher, MsSqlScheduledMessageFetcher>();
        return builder;
    }
}
