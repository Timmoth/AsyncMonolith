using AsyncMonolith.Consumers;
using AsyncMonolith.Ef;
using AsyncMonolith.Tests.Infra;
using AsyncMonolith.Utilities;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;

namespace AsyncMonolith.Tests;

/// <summary>
///     Modules registering their own consumers with AddAsyncMonolithConsumers, alongside the host's
///     single AddEfAsyncMonolith call.
/// </summary>
public class ModuleRegistrationTests
{
    private static ServiceCollection Services()
    {
        var services = new ServiceCollection();
        services.AddSingleton(TimeProvider.System);
        services.AddSingleton<TestConsumerInvocations>();
        services.AddDbContext<TestDbContext>(_ => { });
        return services;
    }

    [Fact]
    public void Module_consumers_registered_before_the_host_are_included()
    {
        var module = ClashingTypes.Build(["Billing"]);
        var services = Services();

        services.AddAsyncMonolithConsumers(module);
        services.AddEfAsyncMonolith<TestDbContext>(s => s.RegisterTypesFromAssemblyContaining<ModuleRegistrationTests>());

        var provider = services.BuildServiceProvider();
        var registry = provider.GetRequiredService<ConsumerRegistry>();
        registry.ResolvePayloadConsumerTypes("SendEmail").ShouldBe(["SendEmailConsumer0"]);
        registry.ResolvePayloadConsumerTypes(nameof(SingleConsumerMessage)).ShouldBe([nameof(SingleConsumer)]);
        using var scope = provider.CreateScope();
        scope.ServiceProvider.GetRequiredService(module.GetType("Billing.SendEmailConsumer0")!).ShouldNotBeNull();
    }

    [Fact]
    public void Module_consumers_registered_after_the_host_are_included()
    {
        var module = ClashingTypes.Build(["Billing"]);
        var services = Services();

        services.AddEfAsyncMonolith<TestDbContext>(s => s.RegisterTypesFromAssemblyContaining<ModuleRegistrationTests>());
        services.AddAsyncMonolithConsumers(module);

        var registry = services.BuildServiceProvider().GetRequiredService<ConsumerRegistry>();
        registry.ResolvePayloadConsumerTypes("SendEmail").ShouldBe(["SendEmailConsumer0"]);
    }

    [Fact]
    public void Registering_the_same_assembly_twice_is_harmless()
    {
        var services = Services();

        services.AddEfAsyncMonolith<TestDbContext>(s => s.RegisterTypesFromAssemblyContaining<ModuleRegistrationTests>());
        services.AddAsyncMonolithConsumersFromAssemblyContaining<ModuleRegistrationTests>();

        services.Count(d => d.ServiceType == typeof(SingleConsumer)).ShouldBe(1);
        services.BuildServiceProvider().GetRequiredService<ConsumerRegistry>()
            .ResolvePayloadConsumerTypes(nameof(SingleConsumerMessage)).ShouldBe([nameof(SingleConsumer)]);
    }

    [Fact]
    public void Payload_without_a_consumer_in_any_module_fails_when_the_host_starts()
    {
        var services = Services();
        services.AddEfAsyncMonolith<TestDbContext>(s => s.RegisterTypesFromAssemblyContaining<ModuleRegistrationTests>());
        services.AddAsyncMonolithConsumers(ClashingTypes.PayloadOnly("Billing"));

        var exception = Should.Throw<InvalidOperationException>(() =>
            services.BuildServiceProvider().GetRequiredService<ConsumerRegistry>());

        exception.Message.ShouldContain("Orphan");
    }

    [Fact]
    public void Rails_are_read_from_every_module()
    {
        var services = Services();
        services.AddEfAsyncMonolith<TestDbContext>(_ => { });
        services.AddAsyncMonolithConsumersFromAssemblyContaining<ModuleRegistrationTests>();

        services.BuildServiceProvider().GetRequiredService<ConsumerRegistry>().RailIds.ShouldBe([0, 1, 2]);
    }

    [Fact]
    public void Rail_zero_is_used_when_there_are_no_consumers()
    {
        var services = Services();
        services.AddEfAsyncMonolith<TestDbContext>(_ => { });

        services.BuildServiceProvider().GetRequiredService<ConsumerRegistry>().RailIds.ShouldBe([0]);
    }
}
