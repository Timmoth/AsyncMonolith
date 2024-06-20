using AsyncMonolith.Consumers;
using AsyncMonolith.Utilities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace AsyncMonolith.Ef;

/// <summary>
/// Fetches consumer messages using Entity Framework.
/// </summary>
public sealed class EfConsumerMessageFetcher : IConsumerMessageFetcher
{
    private readonly IOptions<AsyncMonolithSettings> _options;

    /// <summary>
    /// Initializes a new instance of the <see cref="EfConsumerMessageFetcher"/> class.
    /// </summary>
    /// <param name="options">The options for AsyncMonolithSettings.</param>
    public EfConsumerMessageFetcher(IOptions<AsyncMonolithSettings> options)
    {
        _options = options;
    }

    /// <summary>
    /// Fetches consumer messages from the database.
    /// </summary>
    /// <param name="consumerSet">The DbSet of consumer messages.</param>
    /// <param name="currentTime">The current time.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A task that represents the asynchronous operation. The task result contains a list of consumer messages.</returns>
    public Task<List<ConsumerMessage>> Fetch(DbSet<ConsumerMessage> consumerSet, long currentTime,
        CancellationToken cancellationToken = default)
    {
        return consumerSet
            .Where(m => m.AvailableAfter <= currentTime)
            .OrderBy(m => m.CreatedAt)
            .Take(_options.Value.ProcessorBatchSize)
            .ToListAsync(cancellationToken);
    }
}
