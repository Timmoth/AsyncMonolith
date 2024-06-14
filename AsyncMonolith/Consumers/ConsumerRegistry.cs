using AsyncMonolith.Utilities;

namespace AsyncMonolith.Consumers;

/// <summary>
///     Represents a registry for consumers and their associated types and payloads.
/// </summary>
public sealed class ConsumerRegistry
{
    /// <summary>
    ///     Gets the async monolith settings.
    /// </summary>
    private readonly AsyncMonolithSettings _settings;

    /// <summary>
    ///     Gets the dictionary that maps consumer names to their associated time out.
    /// </summary>
    public readonly IReadOnlyDictionary<string, int> ConsumerTimeoutDictionary;

    /// <summary>
    ///     Gets the dictionary that maps consumer names to their associated types.
    /// </summary>
    public readonly IReadOnlyDictionary<string, Type> ConsumerTypeDictionary;

    /// <summary>
    ///     Gets the dictionary that maps payload types to the list of consumer names that can handle them.
    /// </summary>
    public readonly IReadOnlyDictionary<string, List<string>> PayloadConsumerDictionary;

    /// <summary>
    ///     Initializes a new instance of the <see cref="ConsumerRegistry" /> class.
    /// </summary>
    /// <param name="consumerTypeDictionary">The dictionary that maps consumer names to their associated types.</param>
    /// <param name="payloadConsumerDictionary">
    ///     The dictionary that maps payload types to the list of consumer names that can
    ///     handle them.
    /// </param>
    /// <param name="consumerTimeoutDictionary">The dictionary that maps consumer names to their associated time out.</param>
    /// <param name="settings">Async Monolith settings.</param>
    public ConsumerRegistry(IReadOnlyDictionary<string, Type> consumerTypeDictionary,
        IReadOnlyDictionary<string, List<string>> payloadConsumerDictionary,
        IReadOnlyDictionary<string, int> consumerTimeoutDictionary, AsyncMonolithSettings settings)
    {
        ConsumerTypeDictionary = consumerTypeDictionary;
        PayloadConsumerDictionary = payloadConsumerDictionary;
        ConsumerTimeoutDictionary = consumerTimeoutDictionary;
        _settings = settings;
    }

    /// <summary>
    ///     Resolves the list of consumer names that can handle the specified payload type.
    /// </summary>
    /// <param name="payloadType">The payload type.</param>
    /// <returns>The list of consumer names that can handle the specified payload type.</returns>
    /// <exception cref="Exception">Thrown when no consumers are found for the specified payload type.</exception>
    public IReadOnlyList<string> ResolvePayloadConsumerTypes(string payloadType)
    {
        if (!PayloadConsumerDictionary.TryGetValue(payloadType, out var names))
        {
            throw new Exception($"Failed to resolve consumers for payload: {payloadType}");
        }

        return names;
    }

    /// <summary>
    ///     Resolves the consumer type for the specified consumer message.
    /// </summary>
    /// <param name="consumer">The consumer message.</param>
    /// <returns>The consumer type.</returns>
    /// <exception cref="Exception">Thrown when the consumer type cannot be resolved.</exception>
    public Type ResolveConsumerType(ConsumerMessage consumer)
    {
        if (!ConsumerTypeDictionary.TryGetValue(consumer.ConsumerType, out var type))
        {
            throw new Exception($"Couldn't resolve consumer type: '{consumer.ConsumerType}'");
        }

        return type;
    }

    /// <summary>
    ///     Resolves the consumer timeout for the given consumer message.
    /// </summary>
    /// <param name="consumer">The consumer message.</param>
    /// <returns>The consumer timeout.</returns>
    /// <exception cref="Exception">Thrown when the consumer type cannot be resolved.</exception>
    public int ResolveConsumerTimeout(ConsumerMessage consumer)
    {
        if (ConsumerTimeoutDictionary.TryGetValue(consumer.ConsumerType, out var timeout))
        {
            return timeout;
        }

        return _settings.DefaultConsumerTimeout;
    }

    /// <summary>
    ///     Resolves the consumer timeout for the given consumer type name.
    /// </summary>
    /// <param name="consumerType">The consumer type name.</param>
    /// <returns>The consumer timeout.</returns>
    /// <exception cref="Exception">Thrown when the consumer type cannot be resolved.</exception>
    public int ResolveConsumerTimeout(string consumerType)
    {
        if (ConsumerTimeoutDictionary.TryGetValue(consumerType, out var timeout))
        {
            return timeout;
        }

        return _settings.DefaultConsumerTimeout;
    }
}