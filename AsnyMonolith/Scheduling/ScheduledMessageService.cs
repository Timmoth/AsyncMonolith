using System.Text.Json;
using AsnyMonolith.Consumers;
using AsnyMonolith.Utilities;
using Cronos;
using Microsoft.EntityFrameworkCore;

namespace AsnyMonolith.Scheduling;

public sealed class ScheduledMessageService<T> where T : DbContext
{
    private readonly T _dbContext;
    private readonly IAsyncMonolithIdGenerator _idGenerator;
    private readonly TimeProvider _timeProvider;

    public ScheduledMessageService(TimeProvider timeProvider, T dbContext, IAsyncMonolithIdGenerator idGenerator)
    {
        _timeProvider = timeProvider;
        _dbContext = dbContext;
        _idGenerator = idGenerator;
    }

    public string Schedule<TK>(TK message, string chronExpression, string chronTimezone, params string[] tags)
        where TK : IConsumerPayload
    {
        var payload = JsonSerializer.Serialize(message);
        var id = _idGenerator.GenerateId();

        var expression = CronExpression.Parse(chronExpression, CronFormat.IncludeSeconds);
        if (expression == null)
            throw new InvalidOperationException(
                $"Couldn't determine scheduled message chron expression: '{chronExpression}'");
        var timezone = TimeZoneInfo.FindSystemTimeZoneById(chronTimezone);
        if (timezone == null)
            throw new InvalidOperationException($"Couldn't determine scheduled message timezone: '{chronTimezone}'");
        var next = expression.GetNextOccurrence(_timeProvider.GetUtcNow(), timezone);
        if (next == null) throw new InvalidOperationException("Couldn't determine next scheduled message occurrence");

        _dbContext.Set<ScheduledMessage>().Add(new ScheduledMessage
        {
            Id = id,
            PayloadType = typeof(TK).Name,
            AvailableAfter = next.Value.ToUnixTimeSeconds(),
            Tags = tags,
            ChronExpression = chronExpression,
            ChronTimezone = chronTimezone,
            Payload = payload
        });

        return id;
    }

    public async Task DeleteByTag(string tag, CancellationToken cancellationToken)
    {
        var set = _dbContext.Set<ScheduledMessage>();
        await set.Where(t => t.Tags.Contains(tag)).ExecuteDeleteAsync(cancellationToken);
    }

    public async Task DeleteById(string id, CancellationToken cancellationToken)
    {
        var set = _dbContext.Set<ScheduledMessage>();
        await set.Where(t => t.Id == id).ExecuteDeleteAsync(cancellationToken);
    }
}