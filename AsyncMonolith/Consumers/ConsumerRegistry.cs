namespace AsyncMonolith.Consumers;

public sealed class ConsumerRegistry
{
    public readonly IReadOnlyDictionary<string, Type> ConsumerTypeDictionary;
    public readonly IReadOnlyDictionary<string, List<string>> PayloadConsumerDictionary;

    public ConsumerRegistry(IReadOnlyDictionary<string, Type> consumerTypeDictionary,
        IReadOnlyDictionary<string, List<string>> payloadConsumerDictionary)
    {
        ConsumerTypeDictionary = consumerTypeDictionary;
        PayloadConsumerDictionary = payloadConsumerDictionary;
    }

    public List<string> ResolvePayloadConsumerTypes(string payloadType)
    {
        if (!PayloadConsumerDictionary.TryGetValue(payloadType, out var names))
            throw new Exception($"Failed to resolve consumers for payload: {payloadType}");

        return names;
    }

    public Type ResolveConsumerType(ConsumerMessage consumer)
    {
        if (!ConsumerTypeDictionary.TryGetValue(consumer.ConsumerType, out var type))
            throw new Exception($"Couldn't resolve consumer type: '{consumer.ConsumerType}'");

        return type;
    }
}