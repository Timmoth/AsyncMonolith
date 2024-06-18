using System.Text.Json;
using AsyncMonolith.Consumers;
using AsyncMonolith.Utilities;
using Cronos;
using Microsoft.EntityFrameworkCore;

namespace AsyncMonolith.Scheduling;

/// <summary>
///     Service for scheduling messages.
/// </summary>
/// <typeparam name="T">The type of DbContext.</typeparam>
public sealed class ScheduleService<T> : IScheduleService where T : DbContext
{
    private readonly T _dbContext;
    private readonly IAsyncMonolithIdGenerator _idGenerator;
    private readonly TimeProvider _timeProvider;

    /// <summary>
    ///     Initializes a new instance of the <see cref="ScheduleService{T}" /> class.
    /// </summary>
    /// <param name="timeProvider">The time provider.</param>
    /// <param name="dbContext">The DbContext.</param>
    /// <param name="idGenerator">The ID generator.</param>
    public ScheduleService(TimeProvider timeProvider, T dbContext, IAsyncMonolithIdGenerator idGenerator)
    {
        _timeProvider = timeProvider;
        _dbContext = dbContext;
        _idGenerator = idGenerator;
    }

    public string Schedule<TK>(TK message, string chronExpression, string chronTimezone, string? tag = null)
        where TK : IConsumerPayload
    {
        var payload = JsonSerializer.Serialize(message);
        var id = _idGenerator.GenerateId();

        var expression = CronExpression.Parse(chronExpression, CronFormat.IncludeSeconds);
        if (expression == null)
        {
            throw new InvalidOperationException(
                $"Couldn't determine scheduled message chron expression: '{chronExpression}'");
        }

        var timezone = TimeZoneInfo.FindSystemTimeZoneById(chronTimezone);
        if (timezone == null)
        {
            throw new InvalidOperationException($"Couldn't determine scheduled message timezone: '{chronTimezone}'");
        }

        var next = expression.GetNextOccurrence(_timeProvider.GetUtcNow(), timezone);
        if (next == null)
        {
            throw new InvalidOperationException(
                $"Couldn't determine next scheduled message occurrence for chron expression: '{chronExpression}', timezone: '{chronTimezone}'");
        }

        _dbContext.Set<ScheduledMessage>().Add(new ScheduledMessage
        {
            Id = id,
            PayloadType = typeof(TK).Name,
            AvailableAfter = next.Value.ToUnixTimeSeconds(),
            Tag = tag,
            ChronExpression = chronExpression,
            ChronTimezone = chronTimezone,
            Payload = payload
        });

        return id;
    }

    public async Task DeleteByTag(string tag, CancellationToken cancellationToken = default)
    {
        var set = _dbContext.Set<ScheduledMessage>();
        var messages = await set.Where(t => t.Tag == tag).ToListAsync(cancellationToken);
        set.RemoveRange(messages);
    }

    public async Task DeleteById(string id, CancellationToken cancellationToken = default)
    {
        var set = _dbContext.Set<ScheduledMessage>();
        var message = await set.Where(t => t.Id == id).FirstOrDefaultAsync(cancellationToken);
        if (message != null)
        {
            set.Remove(message);
        }
    }
}