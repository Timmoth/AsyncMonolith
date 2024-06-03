using System.Text.Json;
using AsnyMonolith.Consumers;
using AsnyMonolith.Utilities;
using Microsoft.EntityFrameworkCore;

namespace AsnyMonolith.Scheduling;

public sealed class ScheduledMessageService<T> where T : DbContext
{
    private readonly T _dbContext;
    private readonly IAsnyMonolithIdGenerator _idGenerator;
    private readonly TimeProvider _timeProvider;

    public ScheduledMessageService(TimeProvider timeProvider, T dbContext, IAsnyMonolithIdGenerator idGenerator)
    {
        _timeProvider = timeProvider;
        _dbContext = dbContext;
        _idGenerator = idGenerator;
    }

    public void Schedule<TK>(TK message, long delay, params string[] tags) where TK : IConsumerPayload
    {
        var currentTime = _timeProvider.GetUtcNow().ToUnixTimeSeconds();
        var payload = JsonSerializer.Serialize(message);

        _dbContext.Set<ScheduledMessage>().Add(new ScheduledMessage
        {
            Id = _idGenerator.GenerateId(),
            PayloadType = typeof(TK).Name,
            AvailableAfter = currentTime + delay,
            Tags = tags,
            Delay = delay,
            Payload = payload
        });
    }

    public async Task DeleteByTag(string tag, CancellationToken cancellationToken)
    {
        var set = _dbContext.Set<ScheduledMessage>();
        await set.Where(t => t.Tags.Contains(tag)).ExecuteDeleteAsync(cancellationToken);
    }
}