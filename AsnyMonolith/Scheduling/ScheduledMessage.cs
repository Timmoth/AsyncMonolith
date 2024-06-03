using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json.Serialization;

namespace AsnyMonolith.Scheduling;

[Table("scheduled_messages")]
public class ScheduledMessage
{
    [Key]
    [JsonPropertyName("id")]
    [Column("id")]
    public required string Id { get; set; }

    [JsonPropertyName("tags")]
    [Column("tags")]
    public required string[] Tags { get; set; }

    [JsonPropertyName("available_after")]
    [Column("available_after")]
    public required long AvailableAfter { get; set; }

    [JsonPropertyName("delay")]
    [Column("delay")]
    public required long Delay { get; set; }

    [JsonPropertyName("payload_type")]
    [Column("payload_type")]
    public required string PayloadType { get; set; }

    [JsonPropertyName("payload")]
    [Column("payload")]
    public required string Payload { get; set; }
}