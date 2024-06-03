namespace AsnyMonolith.Consumers;

public sealed class ConsumerRegistry
{
    public readonly IReadOnlyDictionary<string, List<string>> ConsumerNameDictionary;
    public readonly IReadOnlyDictionary<string, Type> ConsumerTypeDictionary;

    public ConsumerRegistry(IReadOnlyDictionary<string, Type> consumerTypeDictionary,
        IReadOnlyDictionary<string, List<string>> consumerNameDictionary)
    {
        ConsumerTypeDictionary = consumerTypeDictionary;
        ConsumerNameDictionary = consumerNameDictionary;
    }

    public List<string> ResolvePayloadConsumerTypes(string payloadType)
    {
        if (!ConsumerNameDictionary.TryGetValue(payloadType, out var names))
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