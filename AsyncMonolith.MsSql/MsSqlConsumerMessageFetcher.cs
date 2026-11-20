using System.Globalization;
using AsyncMonolith.Consumers;
using AsyncMonolith.Utilities;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace AsyncMonolith.MsSql;

/// <summary>
/// Represents a message fetcher for consuming messages from a SQL Server database.
/// </summary>
internal sealed class MsSqlConsumerMessageFetcher : IConsumerMessageFetcher
{
    private const string MsSql = @"
                SELECT TOP (@batchSize) * 
                FROM {table} WITH (ROWLOCK, READPAST)
                WHERE available_after <= @currentTime AND rail_id = @railId
                ORDER BY created_at, id";

    private readonly IOptions<AsyncMonolithOptions> _options;

    /// <summary>
    /// Initializes a new instance of the <see cref="MsSqlConsumerMessageFetcher"/> class.
    /// </summary>
    /// <param name="options">The options for the AsyncMonolith settings.</param>
    public MsSqlConsumerMessageFetcher(IOptions<AsyncMonolithOptions> options)
    {
        _options = options;
    }

    /// <summary>
    /// Fetches a batch of consumer messages from the database.
    /// </summary>
    /// <param name="consumerSet">The <see cref="DbSet{TEntity}"/> of consumer messages.</param>
    /// <param name="currentTime">The current time.</param>
    /// <param name="railId">The message rail to fetch messages from.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A task that represents the asynchronous operation. The task result contains the list of fetched consumer messages.</returns>
    public Task<List<ConsumerMessage>> Fetch(DbSet<ConsumerMessage> consumerSet, long currentTime, int railId,
        CancellationToken cancellationToken = default)
    {
        return consumerSet
            .FromSqlRaw(MsSql.Replace("{table}", AsyncMonolithTables.Name(consumerSet), StringComparison.Ordinal), new SqlParameter("@currentTime", currentTime),
                new SqlParameter("@railId", railId),
                new SqlParameter("@batchSize", _options.Value.BatchSize))
            .ToListAsync(cancellationToken);
    }
}
