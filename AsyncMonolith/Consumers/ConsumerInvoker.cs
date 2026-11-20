using System.Collections.Concurrent;
using System.Reflection;
using System.Text.Json;

namespace AsyncMonolith.Consumers;

/// <summary>
///     Finds the payload type of a consumer and calls it with a deserialized payload.
/// </summary>
internal static class ConsumerInvoker
{
    private sealed record Invoker(
        Type PayloadType,
        Func<object, object, ConsumeContext, CancellationToken, Task> Invoke);

    private static readonly ConcurrentDictionary<Type, Invoker> Invokers = new();

    private static readonly MethodInfo InvokeTypedMethod =
        typeof(ConsumerInvoker).GetMethod(nameof(InvokeTyped), BindingFlags.NonPublic | BindingFlags.Static)!;

    /// <summary>Gets the payload type a consumer handles.</summary>
    /// <exception cref="InvalidOperationException">The type implements IConsumer&lt;T&gt; more than once.</exception>
    public static Type? PayloadTypeOf(Type consumerType)
    {
        var payloadTypes = consumerType.GetInterfaces()
            .Where(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IConsumer<>))
            .Select(i => i.GetGenericArguments()[0])
            .ToList();

        return payloadTypes.Count switch
        {
            0 => null,
            1 => payloadTypes[0],
            _ => throw new InvalidOperationException(
                $"Consumer: '{consumerType.FullName}' implements IConsumer<T> for more than one payload. " +
                "Use one consumer class per payload.")
        };
    }

    /// <summary>Deserializes the message payload and passes it to the consumer.</summary>
    public static Task Invoke(object consumer, ConsumerMessage message, ConsumeContext context,
        JsonSerializerOptions? serializerOptions, CancellationToken cancellationToken)
    {
        var invoker = Invokers.GetOrAdd(consumer.GetType(), Create);
        var payload = JsonSerializer.Deserialize(message.Payload, invoker.PayloadType, serializerOptions) ??
                      throw new InvalidOperationException(
                          $"Consumer: '{message.ConsumerType}' failed to deserialize payload: '{message.PayloadType}'");
        return invoker.Invoke(consumer, payload, context, cancellationToken);
    }

    private static Invoker Create(Type consumerType)
    {
        var payloadType = PayloadTypeOf(consumerType) ?? throw new InvalidOperationException(
            $"Type: '{consumerType.FullName}' does not implement IConsumer<T>.");
        var invoke = InvokeTypedMethod.MakeGenericMethod(payloadType)
            .CreateDelegate<Func<object, object, ConsumeContext, CancellationToken, Task>>();
        return new Invoker(payloadType, invoke);
    }

    private static Task InvokeTyped<TPayload>(object consumer, object payload, ConsumeContext context,
        CancellationToken cancellationToken) where TPayload : IConsumerPayload =>
        ((IConsumer<TPayload>)consumer).Consume((TPayload)payload, context, cancellationToken);
}
