namespace AsyncMonolith.Consumers;

/// <summary>
///     Details of the message being consumed and of this attempt.
/// </summary>
public sealed record ConsumeContext
{
    /// <summary>The message id. Each consumer of a payload gets its own message, with its own id.</summary>
    public required string MessageId { get; init; }

    /// <summary>
    ///     The deduplication key the message was produced with, or the generated key when none was given. All
    ///     consumers of one produced payload share it.
    /// </summary>
    public required string DeduplicationKey { get; init; }

    /// <summary>This attempt's number, starting at 1.</summary>
    public required int Attempt { get; init; }

    /// <summary>The number of attempts allowed before the message is poisoned.</summary>
    public required int MaxAttempts { get; init; }

    /// <summary>True when the message will be poisoned if this attempt fails.</summary>
    public bool IsLastAttempt => Attempt >= MaxAttempts;

    /// <summary>When the message was produced.</summary>
    public required DateTimeOffset CreatedAt { get; init; }

    /// <summary>The stored name of the consumer, see <see cref="AsyncMonolithNames" />.</summary>
    public required string ConsumerName { get; init; }

    /// <summary>The stored name of the payload, see <see cref="AsyncMonolithNames" />.</summary>
    public required string PayloadName { get; init; }

    /// <summary>The trace id of the activity that produced the message, if any.</summary>
    public string? TraceId { get; init; }

    internal static ConsumeContext For(ConsumerMessage message, int maxAttempts) => new()
    {
        MessageId = message.Id,
        DeduplicationKey = message.DeduplicationKey,
        Attempt = Math.Max(message.Attempts, 1),
        MaxAttempts = maxAttempts,
        CreatedAt = DateTimeOffset.FromUnixTimeSeconds(message.CreatedAt),
        ConsumerName = message.ConsumerType,
        PayloadName = message.PayloadType,
        TraceId = message.TraceId
    };
}
