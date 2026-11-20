using AsyncMonolith.Consumers;

namespace AsyncMonolith.Scheduling;

/// <summary>
///     Service for scheduling messages.
/// </summary>
public interface IScheduleService
{
    /// <summary>
    ///     Schedules a message.
    /// </summary>
    /// <typeparam name="TPayload">The type of the message.</typeparam>
    /// <param name="message">The message to schedule.</param>
    /// <param name="cronExpression">A cron expression with 5 fields, or 6 with seconds first.</param>
    /// <param name="timeZone">The timezone for the cron expression.</param>
    /// <param name="tag">The optional tag for the scheduled message.</param>
    /// <returns>The ID of the scheduled message.</returns>
    public string Schedule<TPayload>(TPayload message, string cronExpression, string timeZone, string? tag = null)
        where TPayload : IConsumerPayload;

    /// <summary>
    ///     Creates the schedule with this tag, or updates it if it already exists, so it can be called at every
    ///     startup. If the expression and timezone are unchanged, the next occurrence is kept; otherwise it is
    ///     recalculated. Extra schedules with the same tag are deleted. Changes are tracked, not saved: call
    ///     SaveChanges on your DbContext.
    /// </summary>
    /// <typeparam name="TPayload">The type of the message payload.</typeparam>
    /// <param name="message">The message payload.</param>
    /// <param name="cronExpression">A cron expression with 5 fields, or 6 with seconds first.</param>
    /// <param name="timeZone">The timezone the expression is evaluated in.</param>
    /// <param name="tag">The tag that identifies this schedule.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The id of the schedule.</returns>
    public Task<string> EnsureSchedule<TPayload>(TPayload message, string cronExpression, string timeZone, string tag,
        CancellationToken cancellationToken = default) where TPayload : IConsumerPayload;

    /// <summary>
    ///     Deletes scheduled messages by tag.
    /// </summary>
    /// <param name="tag">The tag of the scheduled messages to delete.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    public Task DeleteByTag(string tag, CancellationToken cancellationToken = default);

    /// <summary>
    ///     Deletes a scheduled message by ID.
    /// </summary>
    /// <param name="id">The ID of the scheduled message to delete.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    public Task DeleteById(string id, CancellationToken cancellationToken = default);
}