using System.Reflection;
using System.Reflection.Emit;
using System.Text.Json.Serialization;
using AsyncMonolith.Consumers;

namespace AsyncMonolith.Tests.Infra;

[ConsumerPayloadName("tests.named.payload")]
public class NamedMessage : IConsumerPayload
{
    [JsonPropertyName("name")] public required string Name { get; set; }
}

[ConsumerName("tests.named.consumer")]
public class NamedConsumer(TestConsumerInvocations invocations) : IConsumer<NamedMessage>
{
    public Task Consume(NamedMessage payload, ConsumeContext context, CancellationToken cancellationToken)
    {
        invocations.Increment(nameof(NamedConsumer));
        return Task.CompletedTask;
    }
}

/// <summary>
///     Builds assemblies at runtime whose payload or consumer names clash, as two modules might.
///     They can't live in the test assembly, because every test registers all of its types.
/// </summary>
public static class ClashingTypes
{
    /// <param name="namespaces">One payload and one consumer are emitted per namespace.</param>
    /// <param name="consumerName">Class name for every consumer, or null to make them unique.</param>
    /// <param name="payloadNameAttributes">Optional [ConsumerPayloadName] value per namespace.</param>
    public static Assembly Build(string[] namespaces, string? consumerName = null,
        string?[]? payloadNameAttributes = null)
    {
        var assembly = AssemblyBuilder.DefineDynamicAssembly(new AssemblyName($"Clashing{Guid.NewGuid():N}"),
            AssemblyBuilderAccess.Run);
        var module = assembly.DefineDynamicModule("Clashing");

        for (var i = 0; i < namespaces.Length; i++)
        {
            var payloadBuilder = module.DefineType($"{namespaces[i]}.SendEmail",
                TypeAttributes.Public | TypeAttributes.Class, typeof(object), [typeof(IConsumerPayload)]);
            payloadBuilder.DefineDefaultConstructor(MethodAttributes.Public);
            if (payloadNameAttributes?[i] is { } payloadName)
            {
                payloadBuilder.SetCustomAttribute(new CustomAttributeBuilder(
                    typeof(ConsumerPayloadNameAttribute).GetConstructor([typeof(string)])!, [payloadName]));
            }

            var payload = payloadBuilder.CreateType();

            var consumerBuilder = module.DefineType($"{namespaces[i]}.{consumerName ?? $"SendEmailConsumer{i}"}",
                TypeAttributes.Public | TypeAttributes.Class, typeof(object));
            consumerBuilder.DefineDefaultConstructor(MethodAttributes.Public);
            ImplementConsumer(consumerBuilder, payload, "Consume");
            consumerBuilder.CreateType();
        }

        return assembly;
    }

    /// <summary>Builds an assembly with one consumer class that implements IConsumer&lt;T&gt; for two payloads.</summary>
    public static Assembly ConsumerOfTwoPayloads()
    {
        var assembly = AssemblyBuilder.DefineDynamicAssembly(new AssemblyName($"TwoPayloads{Guid.NewGuid():N}"),
            AssemblyBuilderAccess.Run);
        var module = assembly.DefineDynamicModule("TwoPayloads");
        string[] names = ["First", "Second"];
        var payloads = names.Select(name =>
        {
            var builder = module.DefineType($"TwoPayloads.{name}", TypeAttributes.Public | TypeAttributes.Class,
                typeof(object), [typeof(IConsumerPayload)]);
            builder.DefineDefaultConstructor(MethodAttributes.Public);
            return builder.CreateType();
        }).ToList();

        var consumer = module.DefineType("TwoPayloads.BothConsumer", TypeAttributes.Public | TypeAttributes.Class,
            typeof(object));
        consumer.DefineDefaultConstructor(MethodAttributes.Public);
        ImplementConsumer(consumer, payloads[0], "ConsumeFirst");
        ImplementConsumer(consumer, payloads[1], "ConsumeSecond");
        consumer.CreateType();
        return assembly;
    }

    /// <summary>Implements IConsumer&lt;payload&gt; with a method that does nothing.</summary>
    private static void ImplementConsumer(TypeBuilder consumer, Type payload, string methodName)
    {
        var consumerInterface = typeof(IConsumer<>).MakeGenericType(payload);
        consumer.AddInterfaceImplementation(consumerInterface);
        var consume = consumer.DefineMethod(methodName,
            MethodAttributes.Private | MethodAttributes.Virtual | MethodAttributes.HideBySig |
            MethodAttributes.NewSlot | MethodAttributes.Final,
            typeof(Task), [payload, typeof(ConsumeContext), typeof(CancellationToken)]);
        var il = consume.GetILGenerator();
        il.Emit(OpCodes.Call, typeof(Task).GetProperty(nameof(Task.CompletedTask))!.GetMethod!);
        il.Emit(OpCodes.Ret);
        consumer.DefineMethodOverride(consume, consumerInterface.GetMethod(nameof(IConsumer<IConsumerPayload>.Consume))!);
    }

    /// <summary>Builds an assembly with a payload that has no consumer.</summary>
    public static Assembly PayloadOnly(string ns)
    {
        var assembly = AssemblyBuilder.DefineDynamicAssembly(new AssemblyName($"PayloadOnly{Guid.NewGuid():N}"),
            AssemblyBuilderAccess.Run);
        var payload = assembly.DefineDynamicModule("PayloadOnly").DefineType($"{ns}.Orphan",
            TypeAttributes.Public | TypeAttributes.Class, typeof(object), [typeof(IConsumerPayload)]);
        payload.DefineDefaultConstructor(MethodAttributes.Public);
        payload.CreateType();
        return assembly;
    }
}
