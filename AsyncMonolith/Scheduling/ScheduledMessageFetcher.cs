using AsyncMonolith.Utilities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace AsyncMonolith.Scheduling;

public abstract class ScheduledMessageFetcher
{
    protected readonly IOptions<AsyncMonolithSettings> Options;

    protected ScheduledMessageFetcher(IOptions<AsyncMonolithSettings> options)
    {
        Options = options;
    }

    public abstract Task<List<ScheduledMessage>> Fetch(DbSet<ScheduledMessage> set, long currentTime,
        CancellationToken cancellationToken);
}