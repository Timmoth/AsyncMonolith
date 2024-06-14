using AsyncMonolith.Scheduling;
using AsyncMonolith.Utilities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace AsyncMonolith.Ef;

public sealed class EfScheduledMessageFetcher : IScheduledMessageFetcher
{
    private readonly IOptions<AsyncMonolithSettings> _options;

    public EfScheduledMessageFetcher(IOptions<AsyncMonolithSettings> options)
    {
        _options = options;
    }

    public Task<List<ScheduledMessage>> Fetch(DbSet<ScheduledMessage> set, long currentTime,
        CancellationToken cancellationToken = default)
    {
        return set.Where(m => m.AvailableAfter <= currentTime)
            .OrderBy(m => m.AvailableAfter)
            .Take(_options.Value.ProcessorBatchSize)
            .ToListAsync(cancellationToken);
    }
}