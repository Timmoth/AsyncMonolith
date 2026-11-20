using AsyncMonolith.Scheduling;
using AsyncMonolith.Utilities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using MySqlConnector;

namespace AsyncMonolith.MySql;

/// <summary>
/// Fetches scheduled messages from MySQL database.
/// </summary>
internal sealed class MySqlScheduledMessageFetcher : IScheduledMessageFetcher
{
    private const string MySql = @"
                    SELECT * 
                    FROM scheduled_messages 
                    WHERE available_after <= @currentTime 
                    LIMIT @batchSize 
                    FOR UPDATE SKIP LOCKED";

    private readonly IOptions<AsyncMonolithOptions> _options;

    /// <summary>
    /// Initializes a new instance of the <see cref="MySqlScheduledMessageFetcher"/> class.
    /// </summary>
    /// <param name="options">The options for AsyncMonolith.</param>
    public MySqlScheduledMessageFetcher(IOptions<AsyncMonolithOptions> options)
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
            .FromSqlRaw(MySql, new MySqlParameter("@currentTime", currentTime),
                new MySqlParameter("@batchSize", _options.Value.BatchSize))
            .ToListAsync(cancellationToken);
    }
}
