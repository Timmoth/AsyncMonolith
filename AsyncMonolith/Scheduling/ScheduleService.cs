using System.Text.Json;
using AsyncMonolith.Consumers;
using AsyncMonolith.Utilities;
using Cronos;
using Microsoft.EntityFrameworkCore;

namespace AsyncMonolith.Scheduling;

/// <summary>
/// Service for scheduling messages.
/// </summary>
/// <typeparam name="T">The type of DbContext.</typeparam>
public sealed class ScheduleService<T> : IScheduleService where T : DbContext
{
    private readonly T _dbContext;
    private readonly IAsyncMonolithIdGenerator _idGenerator;
    private readonly TimeProvider _timeProvider;

    /// <summary>
    /// Initializes a new instance of the <see cref="ScheduleService{T}" /> class.
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

    /// <summary>
    /// Schedules a message.
    /// </summary>
    /// <typeparam name="TPayload">The type of the message.</typeparam>
    /// <param name="message">The message to schedule.</param>
    /// <param name="cronExpression">The cron expression for scheduling.</param>
    /// <param name="timeZone">The timezone for scheduling.</param>
    /// <param name="tag">The optional tag for the scheduled message.</param>
    /// <returns>The ID of the scheduled message.</returns>
    public string Schedule<TPayload>(TPayload message, string cronExpression, string timeZone, string? tag = null)
        where TPayload : IConsumerPayload
    {
        var payload = JsonSerializer.Serialize(message);
        var id = _idGenerator.GenerateId();

        var expression = Cronos.CronExpression.Parse(cronExpression, Cronos.CronFormat.IncludeSeconds);

        var timezone = TimeZoneInfo.FindSystemTimeZoneById(timeZone);
        if (timezone == null)
        {
            throw new InvalidOperationException($"Couldn't determine scheduled message timezone: '{timeZone}'");
        }

        var next = expression.GetNextOccurrence(_timeProvider.GetUtcNow(), timezone);
        if (next == null)
        {
            throw new InvalidOperationException(
                $"Couldn't determine next scheduled message occurrence for chron expression: '{cronExpression}', timezone: '{timeZone}'");
        }

        _dbContext.Set<ScheduledMessage>().Add(new ScheduledMessage
        {
            Id = id,
            PayloadType = AsyncMonolithNames.ForPayload<TPayload>(),
            AvailableAfter = next.Value.ToUnixTimeSeconds(),
            Tag = tag,
            CronExpression = cronExpression,
            CronTimeZone = timeZone,
            Payload = payload
        });

        return id;
    }

    /// <inheritdoc />
    public async Task<string> EnsureSchedule<TPayload>(TPayload message, string cronExpression, string timeZone,
        string tag, CancellationToken cancellationToken = default) where TPayload : IConsumerPayload
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(tag);

        var set = _dbContext.Set<ScheduledMessage>();
        var existing = await set.Where(m => m.Tag == tag).OrderBy(m => m.Id).ToListAsync(cancellationToken);
        if (existing.Count == 0)
        {
            return Schedule(message, cronExpression, timeZone, tag);
        }

        var schedule = existing[0];
        set.RemoveRange(existing.Skip(1));

        schedule.UpdatePayload(message);
        if (schedule.CronExpression != cronExpression || schedule.CronTimeZone != timeZone)
        {
            schedule.UpdateSchedule(cronExpression, timeZone, _timeProvider);
        }

        return schedule.Id;
    }

    /// <summary>
    /// Deletes scheduled messages by tag.
    /// </summary>
    /// <param name="tag">The tag of the scheduled messages to delete.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    public async Task DeleteByTag(string tag, CancellationToken cancellationToken = default)
    {
        var set = _dbContext.Set<ScheduledMessage>();
        var messages = await set.Where(t => t.Tag == tag).ToListAsync(cancellationToken);
        set.RemoveRange(messages);
    }

    /// <summary>
    /// Deletes a scheduled message by ID.
    /// </summary>
    /// <param name="id">The ID of the scheduled message to delete.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
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
