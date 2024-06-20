using AsyncMonolith.Consumers;
using AsyncMonolith.Utilities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Npgsql;

namespace AsyncMonolith.PostgreSql;

/// <summary>
/// Represents a consumer message fetcher implementation for PostgreSQL.
/// </summary>
public sealed class PostgreSqlConsumerMessageFetcher : IConsumerMessageFetcher
{
    private const string PgSql = @"
                    SELECT * 
                    FROM consumer_messages 
                    WHERE available_after <= @currentTime 
                    ORDER BY created_at 
                    FOR UPDATE SKIP LOCKED 
                    LIMIT @batchSize";

    private readonly IOptions<AsyncMonolithSettings> _options;

    /// <summary>
    /// Initializes a new instance of the <see cref="PostgreSqlConsumerMessageFetcher"/> class.
    /// </summary>
    /// <param name="options">The options for the AsyncMonolith settings.</param>
    public PostgreSqlConsumerMessageFetcher(IOptions<AsyncMonolithSettings> options)
    {
        _options = options;
    }

    /// <summary>
    /// Fetches a batch of consumer messages from the PostgreSQL database.
    /// </summary>
    /// <param name="consumerSet">The DbSet of consumer messages.</param>
    /// <param name="currentTime">The current time.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A task that represents the asynchronous operation. The task result contains the list of fetched consumer messages.</returns>
    public Task<List<ConsumerMessage>> Fetch(DbSet<ConsumerMessage> consumerSet, long currentTime,
        CancellationToken cancellationToken = default)
    {
        return consumerSet
            .FromSqlRaw(PgSql, new NpgsqlParameter("@currentTime", currentTime),
                new NpgsqlParameter("@batchSize", _options.Value.ProcessorBatchSize))
            .ToListAsync(cancellationToken);
    }
}
