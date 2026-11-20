using AsyncMonolith.Consumers;
using AsyncMonolith.Scheduling;
using AsyncMonolith.Utilities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace AsyncMonolith.MySql;

/// <summary>
///     Fetches due schedules for MySQL, locking the rows and skipping rows other processors have locked. Values are
///     passed as EF parameters, so this works with any EF Core provider for MySQL.
/// </summary>
internal sealed class MySqlScheduledMessageFetcher : IScheduledMessageFetcher
{
    private const string Sql = "SELECT * FROM {table} WHERE available_after <= {0} ORDER BY available_after, id LIMIT {1} FOR UPDATE SKIP LOCKED";

    private readonly IOptions<AsyncMonolithOptions> _options;

    public MySqlScheduledMessageFetcher(IOptions<AsyncMonolithOptions> options)
    {
        _options = options;
    }

    public Task<List<ScheduledMessage>> Fetch(DbSet<ScheduledMessage> scheduledMessageSet, long currentTime,
        CancellationToken cancellationToken = default)
    {
        var sql = Sql.Replace("{table}", AsyncMonolithTables.Name(scheduledMessageSet), StringComparison.Ordinal);
        return scheduledMessageSet.FromSqlRaw(sql, currentTime, _options.Value.BatchSize).ToListAsync(cancellationToken);
    }
}
