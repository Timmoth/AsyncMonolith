using AsyncMonolith.Utilities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace AsyncMonolith.Scheduling;

/// <summary>
///     Base class for fetching scheduled messages.
/// </summary>
public abstract class ScheduledMessageFetcher
{
    protected readonly IOptions<AsyncMonolithSettings> Options;

    protected ScheduledMessageFetcher(IOptions<AsyncMonolithSettings> options)
    {
        Options = options;
    }

    /// <summary>
    ///     Fetches scheduled messages.
    /// </summary>
    /// <param name="set">The DbSet of scheduled messages.</param>
    /// <param name="currentTime">The current time.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A list of fetched scheduled messages.</returns>
    public abstract Task<List<ScheduledMessage>> Fetch(DbSet<ScheduledMessage> set, long currentTime,
        CancellationToken cancellationToken);
}