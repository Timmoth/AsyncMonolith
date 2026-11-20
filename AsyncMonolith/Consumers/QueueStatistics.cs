using AsyncMonolith.Scheduling;
using Microsoft.EntityFrameworkCore;

namespace AsyncMonolith.Consumers;

/// <summary>
///     A snapshot of the message tables, for health checks and dashboards.
/// </summary>
/// <param name="Available">Messages that are due now and waiting for a processor.</param>
/// <param name="Waiting">Messages that are not yet due: delayed, waiting to retry, or leased by a processor.</param>
/// <param name="OldestAvailableAgeSeconds">
///     How long the longest-waiting due message has been due, in seconds, or 0 when none are due. A value that
///     keeps growing means processors are stuck or falling behind.
/// </param>
/// <param name="Poisoned">Messages that failed every attempt.</param>
/// <param name="Schedules">Recurring schedules.</param>
public sealed record QueueStatistics(
    int Available,
    int Waiting,
    long OldestAvailableAgeSeconds,
    int Poisoned,
    int Schedules);

/// <summary>
///     Reads <see cref="QueueStatistics" />.
/// </summary>
public interface IQueueStatisticsService
{
    /// <summary>Reads the current queue statistics.</summary>
    Task<QueueStatistics> Read(CancellationToken cancellationToken = default);
}

/// <summary>
///     Entity Framework implementation of <see cref="IQueueStatisticsService" />.
/// </summary>
/// <typeparam name="T">The DbContext containing the AsyncMonolith tables.</typeparam>
internal sealed class QueueStatisticsService<T> : IQueueStatisticsService where T : DbContext
{
    private readonly T _dbContext;
    private readonly TimeProvider _timeProvider;

    /// <summary>Initializes a new instance of the <see cref="QueueStatisticsService{T}" /> class.</summary>
    public QueueStatisticsService(T dbContext, TimeProvider timeProvider)
    {
        _dbContext = dbContext;
        _timeProvider = timeProvider;
    }

    /// <inheritdoc />
    public async Task<QueueStatistics> Read(CancellationToken cancellationToken = default)
    {
        var now = _timeProvider.GetUtcNow().ToUnixTimeSeconds();
        var messages = _dbContext.Set<ConsumerMessage>().AsNoTracking();

        var available = await messages.CountAsync(m => m.AvailableAfter <= now, cancellationToken);
        var waiting = await messages.CountAsync(m => m.AvailableAfter > now, cancellationToken);
        var oldest = available == 0
            ? (long?)null
            : await messages.Where(m => m.AvailableAfter <= now)
                .MinAsync(m => (long?)m.AvailableAfter, cancellationToken);
        var poisoned = await _dbContext.Set<PoisonedMessage>().CountAsync(cancellationToken);
        var schedules = await _dbContext.Set<ScheduledMessage>().CountAsync(cancellationToken);

        return new QueueStatistics(available, waiting, oldest is null ? 0 : now - oldest.Value, poisoned, schedules);
    }
}
