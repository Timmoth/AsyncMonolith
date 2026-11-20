using System.Text.Json;
using AsyncMonolith.Consumers;
using AsyncMonolith.Ef;
using AsyncMonolith.Producers;
using AsyncMonolith.Scheduling;
using AsyncMonolith.Tests.Infra;
using AsyncMonolith.Utilities;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Shouldly;

namespace AsyncMonolith.Tests;

/// <summary>
///     Settings validation, consumer registration, id generation and service wiring. No database.
/// </summary>
public class ConfigurationTests
{
    private static ConsumerRegistry Registry(Action<AsyncMonolithSettings>? configure = null)
    {
        var settings = AsyncMonolithSettings.Default;
        configure?.Invoke(settings);
        var services = new ServiceCollection();
        services.AddTestServices(DbType.Ef, settings);
        return services.BuildServiceProvider().GetRequiredService<ConsumerRegistry>();
    }

    private static ConsumerMessage MessageFor(string consumerType) => new()
    {
        Id = "id",
        CreatedAt = 0,
        AvailableAfter = 0,
        Attempts = 0,
        ConsumerType = consumerType,
        RailId = 0,
        PayloadType = "payload",
        Payload = "{}",
        InsertId = "insert",
        TraceId = null,
        SpanId = null
    };

    public static TheoryData<string, Action<AsyncMonolithSettings>> InvalidSettings => new()
    {
        { "negative attempt delay", s => s.AttemptDelay = -1 },
        { "zero max attempts", s => s.MaxAttempts = 0 },
        { "zero max delay", s => s.ProcessorMaxDelay = 0 },
        { "negative min delay", s => s.ProcessorMinDelay = -1 },
        { "min delay above max delay", s => { s.ProcessorMinDelay = 500; s.ProcessorMaxDelay = 100; } },
        { "zero batch size", s => s.ProcessorBatchSize = 0 },
        { "zero default timeout", s => s.DefaultConsumerTimeout = 0 },
        { "default timeout over an hour", s => s.DefaultConsumerTimeout = 3601 }
    };

    [Theory]
    [MemberData(nameof(InvalidSettings))]
    public void Invalid_settings_are_rejected_at_startup(string because, Action<AsyncMonolithSettings> configure)
    {
        var settings = AsyncMonolithSettings.Default;
        configure(settings);

        Should.Throw<ArgumentException>(() => new ServiceCollection().InternalConfigureAsyncMonolithSettings(settings),
            because);
    }

    [Fact]
    public void Boundary_settings_are_accepted()
    {
        var settings = new AsyncMonolithSettings
        {
            AttemptDelay = 0,
            MaxAttempts = 1,
            ProcessorMaxDelay = 1,
            ProcessorMinDelay = 0,
            ProcessorBatchSize = 1,
            DefaultConsumerTimeout = 3600
        };
        var services = new ServiceCollection();

        services.InternalConfigureAsyncMonolithSettings(settings);

        var configured = services.BuildServiceProvider().GetRequiredService<IOptions<AsyncMonolithSettings>>().Value;
        configured.AttemptDelay.ShouldBe(0);
        configured.MaxAttempts.ShouldBe(1);
        configured.ProcessorMaxDelay.ShouldBe(1);
        configured.ProcessorMinDelay.ShouldBe(0);
        configured.ProcessorBatchSize.ShouldBe(1);
        configured.DefaultConsumerTimeout.ShouldBe(3600);
    }

    [Fact]
    public void Default_settings_are_valid()
    {
        Should.NotThrow(() =>
            new ServiceCollection().InternalConfigureAsyncMonolithSettings(AsyncMonolithSettings.Default));
    }

    [Fact]
    public void Consumer_attributes_are_read_at_registration()
    {
        var registry = Registry();

        registry.ResolveConsumerTimeout(nameof(TimeoutConsumer)).ShouldBe(1);
        registry.ResolveConsumerMaxAttempts(MessageFor(nameof(ExceptionConsumer2Attempts))).ShouldBe(2);
        registry.ResolveConsumerExecutionMode(nameof(SequentialProbeConsumer))
            .ShouldBe(ConsumerInstanceExecutionMode.Sequential);
        registry.ResolveConsumerRailId(nameof(RailConsumer2)).ShouldBe(2);
    }

    [Fact]
    public void Consumers_without_attributes_use_the_defaults()
    {
        var registry = Registry(s =>
        {
            s.DefaultConsumerTimeout = 42;
            s.MaxAttempts = 7;
        });

        registry.ResolveConsumerTimeout(nameof(ExceptionConsumer)).ShouldBe(42);
        registry.ResolveConsumerMaxAttempts(MessageFor(nameof(ExceptionConsumer))).ShouldBe(7);
        registry.ResolveConsumerExecutionMode(nameof(ExceptionConsumer))
            .ShouldBe(ConsumerInstanceExecutionMode.Parallel);
        registry.ResolveConsumerRailId(nameof(ExceptionConsumer)).ShouldBe(0);
    }

    [Fact]
    public void Non_positive_attribute_values_fall_back_to_the_defaults()
    {
        var registry = Registry(s =>
        {
            s.DefaultConsumerTimeout = 42;
            s.MaxAttempts = 7;
        });

        registry.ResolveConsumerTimeout(nameof(InvalidAttributesConsumer)).ShouldBe(42);
        registry.ResolveConsumerMaxAttempts(MessageFor(nameof(InvalidAttributesConsumer))).ShouldBe(7);
    }

    [Fact]
    public void Unknown_names_fall_back_or_throw()
    {
        var registry = Registry(s =>
        {
            s.DefaultConsumerTimeout = 42;
            s.MaxAttempts = 7;
        });

        Should.Throw<InvalidOperationException>(() => registry.ResolvePayloadConsumerTypes("UnknownPayload"));
        Should.Throw<InvalidOperationException>(() => registry.ResolveConsumerType(MessageFor("UnknownConsumer")));
        registry.ResolveConsumerTimeout("UnknownConsumer").ShouldBe(42);
        registry.ResolveConsumerMaxAttempts(MessageFor("UnknownConsumer")).ShouldBe(7);
        registry.ResolveConsumerRailId("UnknownConsumer").ShouldBe(0);
        registry.ResolveConsumerExecutionMode("UnknownConsumer").ShouldBe(ConsumerInstanceExecutionMode.Parallel);
    }

    [Fact]
    public void ResolveConsumerRailIds_returns_a_rail_per_consumer_in_order()
    {
        Registry().ResolveConsumerRailIds([nameof(RailConsumer2), nameof(RailConsumer0), nameof(RailConsumer1)])
            .ShouldBe([2, 0, 1]);
    }

    [Fact]
    public void Generated_ids_are_twelve_alphanumeric_characters()
    {
        var id = new AsyncMonolithIdGenerator().GenerateId();

        id.Length.ShouldBe(12);
        id.ShouldAllBe(c => char.IsAsciiLetterOrDigit(c));
    }

    [Fact]
    public void Generated_ids_do_not_repeat()
    {
        var generator = new AsyncMonolithIdGenerator();

        var ids = Enumerable.Range(0, 100_000).Select(_ => generator.GenerateId()).ToList();

        ids.Distinct().Count().ShouldBe(ids.Count);
    }

    [Fact]
    public void AddEfAsyncMonolith_registers_the_services_and_background_processors()
    {
        var services = new ServiceCollection();
        services.AddDbContext<TestDbContext>(_ => { });

        services.AddEfAsyncMonolith<TestDbContext>(s =>
        {
            s.RegisterTypesFromAssemblyContaining<ConfigurationTests>();
            s.MaxAttempts = 9;
        });

        services.ShouldContain(d => d.ServiceType == typeof(IProducerService) &&
                                    d.ImplementationType == typeof(ProducerService<TestDbContext>));
        services.ShouldContain(d => d.ServiceType == typeof(IScheduleService) &&
                                    d.ImplementationType == typeof(ScheduleService<TestDbContext>));
        services.ShouldContain(d => d.ServiceType == typeof(IHostedService) &&
                                    d.ImplementationType == typeof(ScheduledMessageProcessor<TestDbContext>));
        services.Count(d => d.ServiceType == typeof(SingleConsumer)).ShouldBe(1);

        // The test assembly declares consumers on rails 0, 1 and 2, so a processor factory runs one per rail.
        services.ShouldContain(d => d.ServiceType == typeof(IHostedService) && d.ImplementationFactory != null);
        services.ShouldNotContain(d => d.ImplementationType == typeof(ConsumerMessageProcessor<TestDbContext>));

        services.BuildServiceProvider().GetRequiredService<IOptions<AsyncMonolithSettings>>().Value.MaxAttempts
            .ShouldBe(9);
    }
}
