using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json;
using System.Text.Json.Serialization;
using AsyncMonolith.Consumers;
using Cronos;

namespace AsyncMonolith.Scheduling;

/// <summary>
///     Represents a scheduled message.
/// </summary>
[Table("scheduled_messages")]
public class ScheduledMessage
{
    /// <summary>
    ///     Gets or sets the ID of the scheduled message.
    /// </summary>
    [Key]
    [JsonPropertyName("id")]
    [Column("id")]
    public required string Id { get; set; }

    /// <summary>
    ///     Gets or sets the tag of the scheduled message.
    /// </summary>
    [JsonPropertyName("tag")]
    [Column("tag")]
    public required string? Tag { get; set; }

    /// <summary>
    ///     Gets or sets the unix second timestamp after which the scheduled message payload will be enqueued.
    /// </summary>
    [JsonPropertyName("available_after")]
    [Column("available_after")]
    public required long AvailableAfter { get; set; }

    /// <summary>
    ///     Gets or sets the cron expression of the scheduled message.
    /// </summary>
    [JsonPropertyName("chron_expression")]
    [Column("chron_expression")]
    public required string CronExpression { get; set; }

    /// <summary>
    ///     Gets or sets the cron timezone of the scheduled message.
    /// </summary>
    [JsonPropertyName("chron_timezone")]
    [Column("chron_timezone")]
    public required string CronTimeZone { get; set; }

    /// <summary>
    ///     Gets or sets the payload type of the scheduled message.
    /// </summary>
    [JsonPropertyName("payload_type")]
    [Column("payload_type")]
    public required string PayloadType { get; set; }

    /// <summary>
    ///     Gets or sets the payload of the scheduled message.
    /// </summary>
    [JsonPropertyName("payload")]
    [Column("payload")]
    public required string Payload { get; set; }

    /// <summary>
    ///     Gets the next occurrence of the scheduled message as a unix second timestamp.
    /// </summary>
    /// <param name="timeProvider">The time provider.</param>
    /// <returns>The next occurrence of the scheduled message in Unix timestamp format.</returns>
    public long GetNextOccurrence(TimeProvider timeProvider)
    {
        var expression = Cronos.CronExpression.Parse(CronExpression, Cronos.CronFormat.IncludeSeconds);

        var timezone = TimeZoneInfo.FindSystemTimeZoneById(CronTimeZone);
        if (timezone == null)
        {
            throw new InvalidOperationException($"Couldn't determine scheduled message timezone: '{CronTimeZone}'");
        }

        var next = expression.GetNextOccurrence(timeProvider.GetUtcNow(), timezone);
        if (next == null)
        {
            throw new InvalidOperationException("Couldn't determine next scheduled message occurrence");
        }

        return next.Value.ToUnixTimeSeconds();
    }

    /// <summary>
    ///     Updates the schedule of the scheduled message.
    /// </summary>
    /// <param name="cronExpression">The new cron expression.</param>
    /// <param name="timeZone">The new cron timezone.</param>
    /// <param name="timeProvider">The time provider.</param>
    public void UpdateSchedule(string cronExpression, string timeZone, TimeProvider timeProvider)
    {
        CronExpression = cronExpression;
        CronTimeZone = timeZone;
        AvailableAfter = GetNextOccurrence(timeProvider);
    }

    /// <summary>
    ///     Updates the payload of the scheduled message.
    /// </summary>
    /// <typeparam name="TPayload">The type of the payload.</typeparam>
    /// <param name="message">The payload message.</param>
    public void UpdatePayload<TPayload>(TPayload message) where TPayload : IConsumerPayload
    {
        var payload = JsonSerializer.Serialize(message);
        Payload = payload;
        PayloadType = AsyncMonolithNames.ForPayload<TPayload>();
    }
}