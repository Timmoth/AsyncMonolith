using AsyncMonolith.Scheduling;
using AsyncMonolith.Utilities;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace AsyncMonolith.MsSql;

/// <summary>
/// Fetches scheduled messages from the MsSql database.
/// </summary>
public sealed class MsSqlScheduledMessageFetcher : IScheduledMessageFetcher
{
    private const string MsSql = @"
                        SELECT TOP (@batchSize) * 
                        FROM scheduled_messages WITH (ROWLOCK, READPAST)
                        WHERE available_after <= @currentTime";

    private readonly IOptions<AsyncMonolithOptions> _options;

    /// <summary>
    /// Initializes a new instance of the <see cref="MsSqlScheduledMessageFetcher"/> class.
    /// </summary>
    /// <param name="options">The options for the AsyncMonolith settings.</param>
    public MsSqlScheduledMessageFetcher(IOptions<AsyncMonolithOptions> options)
    {
        _options = options;
    }

    /// <summary>
    /// Fetches scheduled messages from the database.
    /// </summary>
    /// <param name="scheduledMessageSet">The DbSet of scheduled messages.</param>
    /// <param name="currentTime">The current time.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A task that represents the asynchronous operation. The task result contains a list of scheduled messages.</returns>
    public Task<List<ScheduledMessage>> Fetch(DbSet<ScheduledMessage> scheduledMessageSet, long currentTime,
        CancellationToken cancellationToken = default)
    {
        return scheduledMessageSet
            .FromSqlRaw(MsSql, new SqlParameter("@currentTime", currentTime),
                new SqlParameter("@batchSize", _options.Value.BatchSize))
            .ToListAsync(cancellationToken);
    }
}
