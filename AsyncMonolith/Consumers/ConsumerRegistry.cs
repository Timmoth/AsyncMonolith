namespace AsyncMonolith.Consumers;

/// <summary>
/// Represents a registry for consumers and their associated types and payloads.
/// </summary>
public sealed class ConsumerRegistry
{
    /// <summary>
    /// Gets the dictionary that maps consumer names to their associated types.
    /// </summary>
    public readonly IReadOnlyDictionary<string, Type> ConsumerTypeDictionary;

    /// <summary>
    /// Gets the dictionary that maps payload types to the list of consumer names that can handle them.
    /// </summary>
    public readonly IReadOnlyDictionary<string, List<string>> PayloadConsumerDictionary;

    /// <summary>
    /// Initializes a new instance of the <see cref="ConsumerRegistry"/> class.
    /// </summary>
    /// <param name="consumerTypeDictionary">The dictionary that maps consumer names to their associated types.</param>
    /// <param name="payloadConsumerDictionary">The dictionary that maps payload types to the list of consumer names that can handle them.</param>
    public ConsumerRegistry(IReadOnlyDictionary<string, Type> consumerTypeDictionary,
        IReadOnlyDictionary<string, List<string>> payloadConsumerDictionary)
    {
        ConsumerTypeDictionary = consumerTypeDictionary;
        PayloadConsumerDictionary = payloadConsumerDictionary;
    }

    /// <summary>
    /// Resolves the list of consumer names that can handle the specified payload type.
    /// </summary>
    /// <param name="payloadType">The payload type.</param>
    /// <returns>The list of consumer names that can handle the specified payload type.</returns>
    /// <exception cref="Exception">Thrown when no consumers are found for the specified payload type.</exception>
    public IReadOnlyList<string> ResolvePayloadConsumerTypes(string payloadType)
    {
        if (!PayloadConsumerDictionary.TryGetValue(payloadType, out var names))
            throw new Exception($"Failed to resolve consumers for payload: {payloadType}");

        return names;
    }

    /// <summary>
    /// Resolves the consumer type for the specified consumer message.
    /// </summary>
    /// <param name="consumer">The consumer message.</param>
    /// <returns>The consumer type.</returns>
    /// <exception cref="Exception">Thrown when the consumer type cannot be resolved.</exception>
    public Type ResolveConsumerType(ConsumerMessage consumer)
    {
        if (!ConsumerTypeDictionary.TryGetValue(consumer.ConsumerType, out var type))
            throw new Exception($"Couldn't resolve consumer type: '{consumer.ConsumerType}'");

        return type;
    }
}
