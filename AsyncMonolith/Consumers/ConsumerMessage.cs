using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json.Serialization;

namespace AsyncMonolith.Consumers;

[Table("consumer_messages")]
public sealed class ConsumerMessage
{
    [Key]
    [JsonPropertyName("id")]
    [Column("id")]
    public required string Id { get; set; }

    [JsonPropertyName("created_at")]
    [Column("created_at")]
    public required long CreatedAt { get; set; }

    [JsonPropertyName("available_after")]
    [Column("available_after")]
    public required long AvailableAfter { get; set; }

    [JsonPropertyName("attempts")]
    [Column("attempts")]
    public required int Attempts { get; set; }

    [JsonPropertyName("consumer_type")]
    [Column("consumer_type")]
    public required string ConsumerType { get; set; }

    [JsonPropertyName("payload_type")]
    [Column("payload_type")]
    public required string PayloadType { get; set; }

    [JsonPropertyName("payload")]
    [Column("payload")]
    public required string Payload { get; set; }
}