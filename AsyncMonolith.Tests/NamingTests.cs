using AsyncMonolith.Consumers;
using AsyncMonolith.Tests.Infra;
using AsyncMonolith.Utilities;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;

namespace AsyncMonolith.Tests;

/// <summary>
///     Stored payload and consumer names, and name clashes caught at startup.
/// </summary>
public class NamingTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static void Register(params System.Reflection.Assembly[] assemblies)
    {
        var settings = AsyncMonolithSettings.Default;
        settings.RegisterTypesFromAssemblies(assemblies);
        new ServiceCollection().InternalRegisterAsyncMonolithConsumers(settings);
    }

    [Fact]
    public void Names_default_to_the_class_name()
    {
        AsyncMonolithNames.ForPayload<SingleConsumerMessage>().ShouldBe(nameof(SingleConsumerMessage));
        AsyncMonolithNames.ForConsumer<SingleConsumer>().ShouldBe(nameof(SingleConsumer));
    }

    [Fact]
    public void Attributes_override_the_stored_names()
    {
        AsyncMonolithNames.ForPayload<NamedMessage>().ShouldBe("tests.named.payload");
        AsyncMonolithNames.ForConsumer<NamedConsumer>().ShouldBe("tests.named.consumer");
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Blank_names_are_rejected(string name)
    {
        Should.Throw<ArgumentException>(() => new ConsumerPayloadNameAttribute(name));
        Should.Throw<ArgumentException>(() => new ConsumerNameAttribute(name));
    }

    [Fact]
    public async Task Named_messages_are_stored_and_consumed_by_their_stored_names()
    {
        await using var host = await SqliteTestHost.Create();

        await host.Produce(new NamedMessage { Name = "x" });

        var message = (await host.ConsumerMessages()).ShouldHaveSingleItem();
        message.PayloadType.ShouldBe("tests.named.payload");
        message.ConsumerType.ShouldBe("tests.named.consumer");

        (await host.ConsumerProcessor.ProcessBatch(Ct)).ShouldBe(1);
        host.Invocations.GetInvocationCount(nameof(NamedConsumer)).ShouldBe(1);
    }

    [Fact]
    public async Task Named_schedules_are_stored_and_produced_by_their_stored_names()
    {
        await using var host = await SqliteTestHost.Create();

        await host.Schedule(new NamedMessage { Name = "x" }, "0 * * * * *");
        (await host.ScheduledMessages()).ShouldHaveSingleItem().PayloadType.ShouldBe("tests.named.payload");

        host.Time.Advance(TimeSpan.FromMinutes(1));
        await host.ScheduledProcessor.ProcessBatch(Ct);

        (await host.ConsumerMessages()).ShouldHaveSingleItem().ConsumerType.ShouldBe("tests.named.consumer");
    }

    [Fact]
    public void Payloads_with_the_same_class_name_in_different_namespaces_fail_at_startup()
    {
        var assembly = ClashingTypes.Build(["ModuleA", "ModuleB"]);

        var exception = Should.Throw<InvalidOperationException>(() => Register(assembly));

        exception.Message.ShouldContain("ModuleA.SendEmail");
        exception.Message.ShouldContain("ModuleB.SendEmail");
        exception.Message.ShouldContain("[ConsumerPayloadName]");
    }

    [Fact]
    public void Payload_name_attributes_resolve_a_class_name_clash()
    {
        var assembly = ClashingTypes.Build(["ModuleA", "ModuleB"],
            payloadNameAttributes: ["module-a.send-email", "module-b.send-email"]);

        Should.NotThrow(() => Register(assembly));
    }

    [Fact]
    public void Consumers_with_the_same_class_name_in_different_namespaces_fail_at_startup()
    {
        var assembly = ClashingTypes.Build(["ModuleA", "ModuleB"], consumerName: "SendEmailConsumer",
            payloadNameAttributes: ["module-a.send-email", "module-b.send-email"]);

        var exception = Should.Throw<InvalidOperationException>(() => Register(assembly));

        exception.Message.ShouldContain("ModuleA.SendEmailConsumer");
        exception.Message.ShouldContain("ModuleB.SendEmailConsumer");
        exception.Message.ShouldContain("[ConsumerName]");
    }
}
