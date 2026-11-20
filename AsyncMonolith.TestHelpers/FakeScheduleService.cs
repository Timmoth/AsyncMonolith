using System.Text.Json;
using AsyncMonolith.Consumers;
using AsyncMonolith.Scheduling;
using AsyncMonolith.Utilities;
using Cronos;

namespace AsyncMonolith.TestHelpers;

/// <summary>
/// Represents a fake implementation of the <see cref="IScheduleService"/> interface for testing purposes.
/// </summary>
public sealed class FakeScheduleService : IScheduleService
{
    private readonly IAsyncMonolithIdGenerator _fakeIdGenerator;
    private readonly TimeProvider _timeProvider;

    /// <summary>
    /// Initializes a new instance of the <see cref="FakeScheduleService"/> class.
    /// </summary>
    /// <param name="timeProvider">The time provider.</param>
    /// <param name="fakeIdGenerator">The fake ID generator.</param>
    public FakeScheduleService(TimeProvider timeProvider, IAsyncMonolithIdGenerator fakeIdGenerator)
    {
        _timeProvider = timeProvider;
        _fakeIdGenerator = fakeIdGenerator;
    }

    /// <summary>
    /// Gets or sets the list of created scheduled messages.
    /// </summary>
    public List<ScheduledMessage> CreatedScheduledMessages { get; set; } = new();

    /// <summary>
    /// Gets or sets the list of deleted scheduled message tags.
    /// </summary>
    public List<string> DeletedScheduledMessageTags { get; set; } = new();

    /// <summary>
    /// Gets or sets the list of deleted scheduled message IDs.
    /// </summary>
    public List<string> DeletedScheduledMessageIds { get; set; } = new();

    /// <summary>
    /// Schedules a message for future execution.
    /// </summary>
    /// <typeparam name="TPayload">The type of the message payload.</typeparam>
    /// <param name="message">The message to schedule.</param>
    /// <param name="cronExpression">The cron expression for scheduling the message.</param>
    /// <param name="timeZone">The timezone for scheduling the message.</param>
    /// <param name="tag">The optional tag for the scheduled message.</param>
    /// <returns>The ID of the scheduled message.</returns>
    /// <exception cref="InvalidOperationException">Thrown when the cron expression or timezone is invalid.</exception>
    public string Schedule<TPayload>(TPayload message, string cronExpression, string timeZone, string? tag = null)
        where TPayload : IConsumerPayload
    {
        var payload = JsonSerializer.Serialize(message);
        var id = _fakeIdGenerator.GenerateId();

        var expression = Cronos.CronExpression.Parse(cronExpression, Cronos.CronFormat.IncludeSeconds);

        var timezone = TimeZoneInfo.FindSystemTimeZoneById(timeZone);
        if (timezone == null)
        {
            throw new InvalidOperationException(
                $"Couldn't determine scheduled message timezone: '{timeZone}'");
        }

        var next = expression.GetNextOccurrence(_timeProvider.GetUtcNow(), timezone);
        if (next == null)
        {
            throw new InvalidOperationException(
                $"Couldn't determine next scheduled message occurrence for cron expression: '{cronExpression}', timezone: '{timeZone}'");
        }

        CreatedScheduledMessages.Add(new ScheduledMessage
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

    /// <summary>
    ///     Records the schedule, replacing any recorded schedule with the same tag.
    /// </summary>
    public Task<string> EnsureSchedule<TPayload>(TPayload message, string cronExpression, string timeZone, string tag,
        CancellationToken cancellationToken = default) where TPayload : IConsumerPayload
    {
        CreatedScheduledMessages.RemoveAll(m => m.Tag == tag);
        return Task.FromResult(Schedule(message, cronExpression, timeZone, tag));
    }

    /// <summary>
    /// Deletes scheduled messages by tag.
    /// </summary>
    /// <param name="tag">The tag of the scheduled messages to delete.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    public Task DeleteByTag(string tag, CancellationToken cancellationToken = default)
    {
        DeletedScheduledMessageTags.Add(tag);
        return Task.CompletedTask;
    }

    /// <summary>
    /// Deletes a scheduled message by ID.
    /// </summary>
    /// <param name="id">The ID of the scheduled message to delete.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    public Task DeleteById(string id, CancellationToken cancellationToken = default)
    {
        DeletedScheduledMessageIds.Add(id);
        return Task.CompletedTask;
    }
}
