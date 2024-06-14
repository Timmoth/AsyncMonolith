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
    /// <typeparam name="TK">The type of the message.</typeparam>
    /// <param name="message">The message to schedule.</param>
    /// <param name="chronExpression">The cron expression.</param>
    /// <param name="chronTimezone">The timezone for the cron expression.</param>
    /// <param name="tag">The optional tag for the scheduled message.</param>
    /// <returns>The ID of the scheduled message.</returns>
    public string Schedule<TK>(TK message, string chronExpression, string chronTimezone, string? tag = null)
        where TK : IConsumerPayload;

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