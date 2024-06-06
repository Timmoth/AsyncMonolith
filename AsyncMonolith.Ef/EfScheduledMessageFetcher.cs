using AsyncMonolith.Scheduling;
using AsyncMonolith.Utilities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace AsyncMonolith.Ef;

public class EfScheduledMessageFetcher : ScheduledMessageFetcher
{
    public EfScheduledMessageFetcher(IOptions<AsyncMonolithSettings> options) : base(options)
    {
    }

    public override Task<List<ScheduledMessage>> Fetch(DbSet<ScheduledMessage> set, long currentTime,
        CancellationToken cancellationToken)
    {
        return set.Where(m => m.AvailableAfter <= currentTime)
            .OrderBy(m => m.AvailableAfter)
            .Take(Options.Value.ProcessorBatchSize)
            .ToListAsync(cancellationToken);
    }
}