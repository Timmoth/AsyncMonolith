using Microsoft.EntityFrameworkCore;

namespace AsyncMonolith.Consumers;

/// <summary>
///     Represents an interface for fetching consumer messages.
/// </summary>
public interface IConsumerMessageFetcher
{
    /// <summary>
    ///     Fetches consumer messages.
    /// </summary>
    /// <param name="consumerSet">The DbSet of consumer messages.</param>
    /// <param name="currentTime">The current time as unix timestamp seconds.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A task that represents the asynchronous operation. The task result contains a list of consumer messages.</returns>
    public Task<List<ConsumerMessage>> Fetch(DbSet<ConsumerMessage> consumerSet, long currentTime,
        CancellationToken cancellationToken = default);
}