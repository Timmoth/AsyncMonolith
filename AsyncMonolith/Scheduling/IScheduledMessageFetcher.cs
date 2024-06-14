using Microsoft.EntityFrameworkCore;

namespace AsyncMonolith.Scheduling;

/// <summary>
///     Interface for fetching scheduled messages.
/// </summary>
public interface IScheduledMessageFetcher
{
    /// <summary>
    ///     Fetches scheduled messages.
    /// </summary>
    /// <param name="set">The DbSet of scheduled messages.</param>
    /// <param name="currentTime">The current time.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A list of fetched scheduled messages.</returns>
    public Task<List<ScheduledMessage>> Fetch(DbSet<ScheduledMessage> set, long currentTime,
        CancellationToken cancellationToken = default);
}