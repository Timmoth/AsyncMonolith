using AsyncMonolith.Consumers;
using AsyncMonolith.Scheduling;
using AsyncMonolith.Utilities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.Options;

namespace AsyncMonolith.Database;

/// <summary>
///     Fetches due consumer messages on a rail. On PostgreSQL, SQL Server, MySQL and MariaDB the rows are locked
///     and rows locked by other processors are skipped; other providers use LINQ without locks. Values are passed
///     as EF parameters, so no database driver is referenced.
/// </summary>
internal sealed class ConsumerMessageFetcher(IOptions<AsyncMonolithOptions> options) : IConsumerMessageFetcher
{
    private const string PostgreSql =
        "SELECT * FROM {table} WHERE available_after <= {0} AND rail_id = {1} ORDER BY created_at, id LIMIT {2} FOR UPDATE SKIP LOCKED";

    private const string SqlServer =
        "SELECT TOP ({2}) * FROM {table} WITH (ROWLOCK, UPDLOCK, READPAST) WHERE available_after <= {0} AND rail_id = {1} ORDER BY created_at, id";

    private const string MySql =
        "SELECT * FROM {table} WHERE available_after <= {0} AND rail_id = {1} ORDER BY created_at, id LIMIT {2} FOR UPDATE SKIP LOCKED";

    public Task<List<ConsumerMessage>> Fetch(DbSet<ConsumerMessage> consumerSet, long currentTime, int railId,
        CancellationToken cancellationToken = default)
    {
        var batchSize = options.Value.BatchSize;
        var sql = SqlDialects.For(consumerSet.GetService<ICurrentDbContext>().Context) switch
        {
            SqlDialect.PostgreSql => PostgreSql,
            SqlDialect.SqlServer => SqlServer,
            SqlDialect.MySql => MySql,
            _ => null
        };

        if (sql is null)
        {
            return consumerSet
                .Where(m => m.AvailableAfter <= currentTime && m.RailId == railId)
                .OrderBy(m => m.CreatedAt).ThenBy(m => m.Id)
                .Take(batchSize)
                .ToListAsync(cancellationToken);
        }

        return consumerSet
            .FromSqlRaw(sql.Replace("{table}", AsyncMonolithTables.Name(consumerSet), StringComparison.Ordinal),
                currentTime, railId, batchSize)
            .ToListAsync(cancellationToken);
    }
}

/// <summary>Fetches due schedules, locking rows where the database supports it, like <see cref="ConsumerMessageFetcher" />.</summary>
internal sealed class ScheduledMessageFetcher(IOptions<AsyncMonolithOptions> options) : IScheduledMessageFetcher
{
    private const string PostgreSql =
        "SELECT * FROM {table} WHERE available_after <= {0} ORDER BY available_after, id LIMIT {1} FOR UPDATE SKIP LOCKED";

    private const string SqlServer =
        "SELECT TOP ({1}) * FROM {table} WITH (ROWLOCK, UPDLOCK, READPAST) WHERE available_after <= {0} ORDER BY available_after, id";

    private const string MySql =
        "SELECT * FROM {table} WHERE available_after <= {0} ORDER BY available_after, id LIMIT {1} FOR UPDATE SKIP LOCKED";

    public Task<List<ScheduledMessage>> Fetch(DbSet<ScheduledMessage> scheduledMessageSet, long currentTime,
        CancellationToken cancellationToken = default)
    {
        var batchSize = options.Value.BatchSize;
        var sql = SqlDialects.For(scheduledMessageSet.GetService<ICurrentDbContext>().Context) switch
        {
            SqlDialect.PostgreSql => PostgreSql,
            SqlDialect.SqlServer => SqlServer,
            SqlDialect.MySql => MySql,
            _ => null
        };

        if (sql is null)
        {
            return scheduledMessageSet
                .Where(m => m.AvailableAfter <= currentTime)
                .OrderBy(m => m.AvailableAfter).ThenBy(m => m.Id)
                .Take(batchSize)
                .ToListAsync(cancellationToken);
        }

        return scheduledMessageSet
            .FromSqlRaw(sql.Replace("{table}", AsyncMonolithTables.Name(scheduledMessageSet), StringComparison.Ordinal),
                currentTime, batchSize)
            .ToListAsync(cancellationToken);
    }
}
