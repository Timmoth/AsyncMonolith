using System.Text.Json;
using AsyncMonolith.Consumers;
using AsyncMonolith.Utilities;
using Cronos;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace AsyncMonolith.Scheduling;

/// <summary>
/// Service for scheduling messages.
/// </summary>
/// <typeparam name="T">The type of DbContext.</typeparam>
internal sealed class ScheduleService<T> : IScheduleService where T : DbContext
{
    private readonly T _dbContext;
    private readonly IAsyncMonolithIdGenerator _idGenerator;
    private readonly TimeProvider _timeProvider;
    private readonly IOptions<AsyncMonolithOptions> _options;

    /// <summary>
    /// Initializes a new instance of the <see cref="ScheduleService{T}" /> class.
    /// </summary>
    /// <param name="timeProvider">The time provider.</param>
    /// <param name="dbContext">The DbContext.</param>
    /// <param name="idGenerator">The ID generator.</param>
    /// <param name="options">The AsyncMonolith options.</param>
    public ScheduleService(TimeProvider timeProvider, T dbContext, IAsyncMonolithIdGenerator idGenerator,
        IOptions<AsyncMonolithOptions> options)
    {
        _timeProvider = timeProvider;
        _options = options;
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
        var payload = JsonSerializer.Serialize(message, _options.Value.SerializerOptions);
        var id = _idGenerator.GenerateId();

        var expression = Cron.Parse(cronExpression);

        // Throws TimeZoneNotFoundException for an unknown time zone.
        var timezone = TimeZoneInfo.FindSystemTimeZoneById(timeZone);
        var next = expression.GetNextOccurrence(_timeProvider.GetUtcNow(), timezone) ??
                   throw new InvalidOperationException(
                       $"The cron expression '{cronExpression}' never occurs after now in time zone '{timeZone}'.");

        _dbContext.Set<ScheduledMessage>().Add(new ScheduledMessage
        {
            Id = id,
            PayloadType = AsyncMonolithNames.ForPayload<TPayload>(),
            AvailableAfter = next.ToUnixTimeSeconds(),
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

        schedule.UpdatePayload(message, _options.Value.SerializerOptions);
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
