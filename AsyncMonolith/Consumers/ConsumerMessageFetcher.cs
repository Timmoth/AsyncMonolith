using AsyncMonolith.Utilities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace AsyncMonolith.Consumers;

/// <summary>
///     Represents a base class for fetching consumer messages.
/// </summary>
public abstract class ConsumerMessageFetcher
{
    protected readonly IOptions<AsyncMonolithSettings> Options;

    /// <summary>
    ///     Initializes a new instance of the <see cref="ConsumerMessageFetcher" /> class.
    /// </summary>
    /// <param name="options">The options for the AsyncMonolithSettings.</param>
    protected ConsumerMessageFetcher(IOptions<AsyncMonolithSettings> options)
    {
        Options = options;
    }

    /// <summary>
    ///     Fetches consumer messages.
    /// </summary>
    /// <param name="consumerSet">The DbSet of consumer messages.</param>
    /// <param name="currentTime">The current time as unix timestamp seconds.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A task that represents the asynchronous operation. The task result contains a list of consumer messages.</returns>
    public abstract Task<List<ConsumerMessage>> Fetch(DbSet<ConsumerMessage> consumerSet, long currentTime,
        CancellationToken cancellationToken);
}