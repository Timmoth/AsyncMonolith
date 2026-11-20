using AsyncMonolith.Consumers;
using AsyncMonolith.Scheduling;
using AsyncMonolith.Utilities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace AsyncMonolith.MsSql;

/// <summary>
///     Fetches due schedules for SQL Server, locking the rows and skipping rows other processors have locked. Values are
///     passed as EF parameters, so this works with any EF Core provider for SQL Server.
/// </summary>
internal sealed class MsSqlScheduledMessageFetcher : IScheduledMessageFetcher
{
    private const string Sql = "SELECT TOP ({1}) * FROM {table} WITH (ROWLOCK, UPDLOCK, READPAST) WHERE available_after <= {0} ORDER BY available_after, id";

    private readonly IOptions<AsyncMonolithOptions> _options;

    public MsSqlScheduledMessageFetcher(IOptions<AsyncMonolithOptions> options)
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
