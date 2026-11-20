using System.Collections.Concurrent;
using System.Reflection;

namespace AsyncMonolith.Consumers;

/// <summary>
///     Gives a payload type a stable name to store in <c>payload_type</c>, instead of its class name.
/// </summary>
/// <remarks>
///     Use it when payloads in different modules share a class name, or to rename a class without
///     breaking stored and scheduled messages. For example <c>[ConsumerPayloadName("minicms.email.send")]</c>.
/// </remarks>
[AttributeUsage(AttributeTargets.Class, Inherited = false)]
public sealed class ConsumerPayloadNameAttribute : Attribute
{
    /// <summary>Initializes a new instance of the <see cref="ConsumerPayloadNameAttribute" /> class.</summary>
    /// <param name="name">The stored name. Must not be empty or whitespace.</param>
    public ConsumerPayloadNameAttribute(string name)
    {
        Name = AsyncMonolithNames.Validate(name);
    }

    /// <summary>Gets the stored name.</summary>
    public string Name { get; }
}

/// <summary>
///     Gives a consumer type a stable name to store in <c>consumer_type</c>, instead of its class name.
/// </summary>
[AttributeUsage(AttributeTargets.Class, Inherited = false)]
public sealed class ConsumerNameAttribute : Attribute
{
    /// <summary>Initializes a new instance of the <see cref="ConsumerNameAttribute" /> class.</summary>
    /// <param name="name">The stored name. Must not be empty or whitespace.</param>
    public ConsumerNameAttribute(string name)
    {
        Name = AsyncMonolithNames.Validate(name);
    }

    /// <summary>Gets the stored name.</summary>
    public string Name { get; }
}

/// <summary>
///     Resolves the names stored for payload and consumer types: the name from
///     <see cref="ConsumerPayloadNameAttribute" /> or <see cref="ConsumerNameAttribute" /> when present,
///     otherwise the class name.
/// </summary>
public static class AsyncMonolithNames
{
    private static readonly ConcurrentDictionary<Type, string> PayloadNames = new();
    private static readonly ConcurrentDictionary<Type, string> ConsumerNames = new();

    /// <summary>Gets the stored name for a payload type.</summary>
    public static string ForPayload(Type payloadType) =>
        PayloadNames.GetOrAdd(payloadType,
            t => t.GetCustomAttribute<ConsumerPayloadNameAttribute>()?.Name ?? t.Name);

    /// <summary>Gets the stored name for a payload type.</summary>
    public static string ForPayload<TPayload>() where TPayload : IConsumerPayload => ForPayload(typeof(TPayload));

    /// <summary>Gets the stored name for a consumer type.</summary>
    public static string ForConsumer(Type consumerType) =>
        ConsumerNames.GetOrAdd(consumerType,
            t => t.GetCustomAttribute<ConsumerNameAttribute>()?.Name ?? t.Name);

    /// <summary>Gets the stored name for a consumer type.</summary>
    public static string ForConsumer<TConsumer>() where TConsumer : IConsumer => ForConsumer(typeof(TConsumer));

    internal static string Validate(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException("A stored name must not be empty or whitespace.", nameof(name));
        }

        return name;
    }
}
