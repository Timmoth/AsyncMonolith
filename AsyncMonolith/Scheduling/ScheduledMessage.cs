using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Reflection.Emit;
using System.Text.Json;
using System.Text.Json.Serialization;
using AsyncMonolith.Consumers;
using Cronos;
using Microsoft.EntityFrameworkCore;

namespace AsyncMonolith.Scheduling;

[Table("scheduled_messages")]
public class ScheduledMessage
{
    [Key]
    [JsonPropertyName("id")]
    [Column("id")]
    public required string Id { get; set; }

    [JsonPropertyName("tag")]
    [Column("tag")]
    public required string? Tag { get; set; }

    [JsonPropertyName("available_after")]
    [Column("available_after")]
    public required long AvailableAfter { get; set; }

    [JsonPropertyName("chron_expression")]
    [Column("chron_expression")]
    public required string ChronExpression { get; set; }

    [JsonPropertyName("chron_timezone")]
    [Column("chron_timezone")]
    public required string ChronTimezone { get; set; }

    [JsonPropertyName("payload_type")]
    [Column("payload_type")]
    public required string PayloadType { get; set; }

    [JsonPropertyName("payload")]
    [Column("payload")]
    public required string Payload { get; set; }

    public long GetNextOccurrence(TimeProvider timeProvider)
    {
        var expression = CronExpression.Parse(ChronExpression, CronFormat.IncludeSeconds);
        if (expression == null)
            throw new InvalidOperationException(
                $"Couldn't determine scheduled message chron expression: '{ChronExpression}'");
        var timezone = TimeZoneInfo.FindSystemTimeZoneById(ChronTimezone);
        if (timezone == null)
            throw new InvalidOperationException($"Couldn't determine scheduled message timezone: '{ChronTimezone}'");
        var next = expression.GetNextOccurrence(timeProvider.GetUtcNow(), timezone);
        if (next == null) throw new InvalidOperationException("Couldn't determine next scheduled message occurrence");

        return next.Value.ToUnixTimeSeconds();
    }

    public void UpdateSchedule(string chronExpression, string chronTimezone, TimeProvider timeProvider)
    {
        ChronExpression = chronExpression;
        ChronTimezone = chronTimezone;
        AvailableAfter = GetNextOccurrence(timeProvider);
    }

    public void UpdatePayload<TK>(TK message) where TK : IConsumerPayload
    {
        var payload = JsonSerializer.Serialize(message);
        Payload = payload;
        PayloadType = typeof(TK).Name;
    }
}