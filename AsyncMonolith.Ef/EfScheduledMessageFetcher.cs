using AsyncMonolith.Scheduling;
using AsyncMonolith.Utilities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace AsyncMonolith.Ef;

/// <summary>
/// Fetches scheduled messages from the database using Entity Framework.
/// </summary>
public sealed class EfScheduledMessageFetcher : IScheduledMessageFetcher
{
    private readonly IOptions<AsyncMonolithSettings> _options;

    /// <summary>
    /// Initializes a new instance of the <see cref="EfScheduledMessageFetcher"/> class.
    /// </summary>
    /// <param name="options">The options for AsyncMonolith.</param>
    public EfScheduledMessageFetcher(IOptions<AsyncMonolithSettings> options)
    {
        _options = options;
    }

    /// <summary>
    /// Fetches scheduled messages from the database.
    /// </summary>
    /// <param name="set">The DbSet of scheduled messages.</param>
    /// <param name="currentTime">The current time.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A task representing the asynchronous operation, containing the list of fetched scheduled messages.</returns>
    public Task<List<ScheduledMessage>> Fetch(DbSet<ScheduledMessage> set, long currentTime,
        CancellationToken cancellationToken = default)
    {
        return set.Where(m => m.AvailableAfter <= currentTime)
            .OrderBy(m => m.AvailableAfter)
            .Take(_options.Value.ProcessorBatchSize)
            .ToListAsync(cancellationToken);
    }
}
