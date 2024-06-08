using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json.Serialization;

namespace AsyncMonolith.Consumers;

[Table("poisoned_messages")]
public sealed class PoisonedMessage
{
    /// <summary>
    /// Gets or sets the ID of the consumer message.
    /// </summary>
    [Key]
    [JsonPropertyName("id")]
    [Column("id")]
    public required string Id { get; set; }

    /// <summary>
    /// Gets or sets the unix second timestamp when the consumer message was created.
    /// </summary>
    [JsonPropertyName("created_at")]
    [Column("created_at")]
    public required long CreatedAt { get; set; }

    /// <summary>
    /// Gets or sets the unix second timestamp at which that the consumer message becomes available for processing.
    /// </summary>
    [JsonPropertyName("available_after")]
    [Column("available_after")]
    public required long AvailableAfter { get; set; }

    /// <summary>
    /// Gets or sets the number of attempts made to process the consumer message.
    /// </summary>
    [JsonPropertyName("attempts")]
    [Column("attempts")]
    public required int Attempts { get; set; }

    /// <summary>
    /// Gets or sets the type of consumer that will process the message.
    /// </summary>
    [JsonPropertyName("consumer_type")]
    [Column("consumer_type")]
    public required string ConsumerType { get; set; }

    /// <summary>
    /// Gets or sets the type of payload contained in the consumer message.
    /// </summary>
    [JsonPropertyName("payload_type")]
    [Column("payload_type")]
    public required string PayloadType { get; set; }

    /// <summary>
    /// Gets or sets the payload of the consumer message.
    /// </summary>
    [JsonPropertyName("payload")]
    [Column("payload")]
    public required string Payload { get; set; }

    /// <summary>
    /// Gets or sets the ID of the insert operation that created the consumer message.
    /// Only one insert_id / consumer_type pair will be in the consumer_messages table at any time.
    /// </summary>
    [JsonPropertyName("insert_id")]
    [Column("insert_id")]
    public required string InsertId { get; set; }
}
