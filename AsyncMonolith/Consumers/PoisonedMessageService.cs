using Microsoft.EntityFrameworkCore;

namespace AsyncMonolith.Consumers;

/// <summary>
///     Inspects, retries and deletes poisoned messages: messages that failed every attempt.
///     Changes are saved immediately.
/// </summary>
public interface IPoisonedMessageService
{
    /// <summary>Counts poisoned messages.</summary>
    Task<int> Count(CancellationToken cancellationToken = default);

    /// <summary>Lists poisoned messages, most recently created first.</summary>
    Task<IReadOnlyList<PoisonedMessage>> List(int skip = 0, int take = 50,
        CancellationToken cancellationToken = default);

    /// <summary>
    ///     Moves a poisoned message back to the queue with its attempts reset, available immediately.
    /// </summary>
    /// <returns>False if no poisoned message has that id.</returns>
    Task<bool> Retry(string id, CancellationToken cancellationToken = default);

    /// <summary>Moves every poisoned message back to the queue.</summary>
    /// <returns>The number of messages moved.</returns>
    Task<int> RetryAll(CancellationToken cancellationToken = default);

    /// <summary>Deletes a poisoned message.</summary>
    /// <returns>False if no poisoned message has that id.</returns>
    Task<bool> Delete(string id, CancellationToken cancellationToken = default);
}

/// <summary>
///     Entity Framework implementation of <see cref="IPoisonedMessageService" />.
/// </summary>
/// <typeparam name="T">The DbContext containing the AsyncMonolith tables.</typeparam>
public sealed class PoisonedMessageService<T> : IPoisonedMessageService where T : DbContext
{
    private readonly T _dbContext;
    private readonly TimeProvider _timeProvider;

    /// <summary>Initializes a new instance of the <see cref="PoisonedMessageService{T}" /> class.</summary>
    public PoisonedMessageService(T dbContext, TimeProvider timeProvider)
    {
        _dbContext = dbContext;
        _timeProvider = timeProvider;
    }

    /// <inheritdoc />
    public Task<int> Count(CancellationToken cancellationToken = default) =>
        _dbContext.Set<PoisonedMessage>().CountAsync(cancellationToken);

    /// <inheritdoc />
    public async Task<IReadOnlyList<PoisonedMessage>> List(int skip = 0, int take = 50,
        CancellationToken cancellationToken = default)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(skip);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(take);

        return await _dbContext.Set<PoisonedMessage>()
            .AsNoTracking()
            .OrderByDescending(m => m.CreatedAt)
            .ThenBy(m => m.Id)
            .Skip(skip)
            .Take(take)
            .ToListAsync(cancellationToken);
    }

    /// <inheritdoc />
    public async Task<bool> Retry(string id, CancellationToken cancellationToken = default)
    {
        var poisoned = await _dbContext.Set<PoisonedMessage>().FirstOrDefaultAsync(m => m.Id == id, cancellationToken);
        if (poisoned is null)
        {
            return false;
        }

        Requeue(poisoned);
        await _dbContext.SaveChangesAsync(cancellationToken);
        return true;
    }

    /// <inheritdoc />
    public async Task<int> RetryAll(CancellationToken cancellationToken = default)
    {
        var poisoned = await _dbContext.Set<PoisonedMessage>().ToListAsync(cancellationToken);
        foreach (var message in poisoned)
        {
            Requeue(message);
        }

        await _dbContext.SaveChangesAsync(cancellationToken);
        return poisoned.Count;
    }

    /// <inheritdoc />
    public async Task<bool> Delete(string id, CancellationToken cancellationToken = default) =>
        await _dbContext.Set<PoisonedMessage>().Where(m => m.Id == id).ExecuteDeleteAsync(cancellationToken) > 0;

    private void Requeue(PoisonedMessage poisoned)
    {
        _dbContext.Set<PoisonedMessage>().Remove(poisoned);
        _dbContext.Set<ConsumerMessage>().Add(new ConsumerMessage
        {
            Id = poisoned.Id,
            CreatedAt = poisoned.CreatedAt,
            AvailableAfter = _timeProvider.GetUtcNow().ToUnixTimeSeconds(),
            Attempts = 0,
            ConsumerType = poisoned.ConsumerType,
            RailId = poisoned.RailId,
            PayloadType = poisoned.PayloadType,
            Payload = poisoned.Payload,
            DeduplicationKey = poisoned.DeduplicationKey,
            TraceId = poisoned.TraceId,
            SpanId = poisoned.SpanId,
            LastError = poisoned.LastError
        });
    }
}
