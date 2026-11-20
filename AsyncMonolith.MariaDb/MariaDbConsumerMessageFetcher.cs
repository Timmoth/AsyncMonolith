using AsyncMonolith.Consumers;
using AsyncMonolith.Scheduling;
using AsyncMonolith.Utilities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace AsyncMonolith.MariaDb;

/// <summary>
///     Fetches due consumer messages on this rail for MariaDB, locking the rows and skipping rows other processors have locked. Values are
///     passed as EF parameters, so this works with any EF Core provider for MariaDB.
/// </summary>
internal sealed class MariaDbConsumerMessageFetcher : IConsumerMessageFetcher
{
    private const string Sql = "SELECT * FROM {table} WHERE available_after <= {0} AND rail_id = {1} ORDER BY created_at, id LIMIT {2} FOR UPDATE SKIP LOCKED";

    private readonly IOptions<AsyncMonolithOptions> _options;

    public MariaDbConsumerMessageFetcher(IOptions<AsyncMonolithOptions> options)
    {
        _options = options;
    }

    public Task<List<ConsumerMessage>> Fetch(DbSet<ConsumerMessage> consumerSet, long currentTime, int railId,
        CancellationToken cancellationToken = default)
    {
        var sql = Sql.Replace("{table}", AsyncMonolithTables.Name(consumerSet), StringComparison.Ordinal);
        return consumerSet.FromSqlRaw(sql, currentTime, railId, _options.Value.BatchSize).ToListAsync(cancellationToken);
    }
}
