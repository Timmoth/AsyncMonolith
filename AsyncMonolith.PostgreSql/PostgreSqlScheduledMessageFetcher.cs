using AsyncMonolith.Scheduling;
using AsyncMonolith.Utilities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Npgsql;

namespace AsyncMonolith.PostgreSql;

/// <summary>
/// Fetches scheduled messages from a PostgreSQL database.
/// </summary>
public sealed class PostgreSqlScheduledMessageFetcher : IScheduledMessageFetcher
{
    private const string PgSql = @"
                    SELECT * 
                    FROM scheduled_messages 
                    WHERE available_after <= @currentTime 
                    FOR UPDATE SKIP LOCKED 
                    LIMIT @batchSize";

    private readonly IOptions<AsyncMonolithOptions> _options;

    /// <summary>
    /// Initializes a new instance of the <see cref="PostgreSqlScheduledMessageFetcher"/> class.
    /// </summary>
    /// <param name="options">The options for the AsyncMonolith.</param>
    public PostgreSqlScheduledMessageFetcher(IOptions<AsyncMonolithOptions> options)
    {
        _options = options;
    }

    /// <summary>
    /// Fetches scheduled messages from the database.
    /// </summary>
    /// <param name="scheduledMessageSet">The <see cref="DbSet{TEntity}"/> of scheduled messages.</param>
    /// <param name="currentTime">The current time.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A task that represents the asynchronous operation. The task result contains a list of fetched scheduled messages.</returns>
    public Task<List<ScheduledMessage>> Fetch(DbSet<ScheduledMessage> scheduledMessageSet, long currentTime,
        CancellationToken cancellationToken = default)
    {
        return scheduledMessageSet
            .FromSqlRaw(PgSql, new NpgsqlParameter("@currentTime", currentTime),
                new NpgsqlParameter("@batchSize", _options.Value.BatchSize))
            .ToListAsync(cancellationToken);
    }
}
