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
public class NamedConsumer(TestConsumerInvocations invocations) : BaseConsumer<NamedMessage>
{
    public override Task Consume(NamedMessage payload, CancellationToken cancellationToken)
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

            var baseType = typeof(BaseConsumer<>).MakeGenericType(payload);
            var consumerBuilder = module.DefineType($"{namespaces[i]}.{consumerName ?? $"SendEmailConsumer{i}"}",
                TypeAttributes.Public | TypeAttributes.Class, baseType);
            consumerBuilder.DefineDefaultConstructor(MethodAttributes.Public);
            var consume = consumerBuilder.DefineMethod("Consume",
                MethodAttributes.Public | MethodAttributes.Virtual | MethodAttributes.HideBySig,
                typeof(Task), [payload, typeof(CancellationToken)]);
            var il = consume.GetILGenerator();
            il.Emit(OpCodes.Call, typeof(Task).GetProperty(nameof(Task.CompletedTask))!.GetMethod!);
            il.Emit(OpCodes.Ret);
            consumerBuilder.DefineMethodOverride(consume,
                baseType.GetMethod("Consume", [payload, typeof(CancellationToken)])!);
            consumerBuilder.CreateType();
        }

        return assembly;
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
