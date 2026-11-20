using System.Text.Json;
using AsyncMonolith.Consumers;
using AsyncMonolith.Ef;
using AsyncMonolith.Producers;
using AsyncMonolith.Scheduling;
using AsyncMonolith.Tests.Infra;
using AsyncMonolith.Utilities;
using Microsoft.Extensions.Configuration;
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
    private static ConsumerRegistry Registry(Action<AsyncMonolithOptions>? configure = null)
    {
        var settings = new AsyncMonolithOptions();
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
        DeduplicationKey = "insert",
        TraceId = null,
        SpanId = null
    };

    public static TheoryData<string, Action<AsyncMonolithOptions>> InvalidSettings => new()
    {
        { "negative attempt delay", s => s.AttemptDelay = TimeSpan.FromSeconds(-1) },
        { "zero max attempts", s => s.MaxAttempts = 0 },
        { "negative max attempt delay", s => s.MaxAttemptDelay = TimeSpan.FromSeconds(-1) },
        { "zero idle poll interval", s => s.IdlePollInterval = TimeSpan.Zero },
        { "negative busy poll interval", s => s.BusyPollInterval = TimeSpan.FromMilliseconds(-1) },
        { "busy poll interval above idle", s => { s.BusyPollInterval = TimeSpan.FromMilliseconds(500); s.IdlePollInterval = TimeSpan.FromMilliseconds(100); } },
        { "fractional default timeout", s => s.DefaultConsumerTimeout = TimeSpan.FromMilliseconds(1500) },
        { "zero batch size", s => s.BatchSize = 0 },
        { "zero default timeout", s => s.DefaultConsumerTimeout = TimeSpan.FromSeconds(0) },
        { "default timeout over an hour", s => s.DefaultConsumerTimeout = TimeSpan.FromSeconds(3601) }
    };

    [Theory]
    [MemberData(nameof(InvalidSettings))]
    public void Invalid_settings_are_rejected_at_startup(string because, Action<AsyncMonolithOptions> configure)
    {
        var services = new ServiceCollection();
        services.InternalConfigureAsyncMonolithOptions(configure);

        var exception = Should.Throw<OptionsValidationException>(() =>
            services.BuildServiceProvider().GetRequiredService<IOptions<AsyncMonolithOptions>>().Value, because);
        exception.Message.ShouldStartWith("AsyncMonolith:");
    }

    [Fact]
    public async Task Invalid_options_stop_the_host_from_starting()
    {
        var builder = Microsoft.Extensions.Hosting.Host.CreateApplicationBuilder();
        builder.Services.AddDbContext<TestDbContext>(_ => { });
        builder.Services.AddEfAsyncMonolith<TestDbContext>(s => s.BatchSize = 0);
        using var host = builder.Build();

        await Should.ThrowAsync<OptionsValidationException>(() =>
            host.StartAsync(TestContext.Current.CancellationToken));
    }

    [Fact]
    public void Options_bind_from_configuration_over_code()
    {
        var configuration = new Microsoft.Extensions.Configuration.ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["AsyncMonolith:MaxAttempts"] = "9",
                ["AsyncMonolith:AttemptDelay"] = "00:00:30",
                ["AsyncMonolith:IdlePollInterval"] = "00:00:00.250"
            })
            .Build();
        var services = new ServiceCollection();
        services.AddSingleton<Microsoft.Extensions.Configuration.IConfiguration>(configuration);
        services.AddDbContext<TestDbContext>(_ => { });

        services.AddEfAsyncMonolith<TestDbContext>(s => s.MaxAttempts = 3).BindConfiguration("AsyncMonolith");

        var options = services.BuildServiceProvider().GetRequiredService<IOptions<AsyncMonolithOptions>>().Value;
        options.MaxAttempts.ShouldBe(9);
        options.AttemptDelay.ShouldBe(TimeSpan.FromSeconds(30));
        options.IdlePollInterval.ShouldBe(TimeSpan.FromMilliseconds(250));
        options.BatchSize.ShouldBe(5);
    }

    [Fact]
    public void Boundary_settings_are_accepted()
    {
        var services = new ServiceCollection();

        services.InternalConfigureAsyncMonolithOptions(s =>
        {
            s.AttemptDelay = TimeSpan.Zero;
            s.MaxAttemptDelay = TimeSpan.Zero;
            s.MaxAttempts = 1;
            s.IdlePollInterval = TimeSpan.FromMilliseconds(1);
            s.BusyPollInterval = TimeSpan.Zero;
            s.BatchSize = 1;
            s.DefaultConsumerTimeout = TimeSpan.FromHours(1);
        });

        var configured = services.BuildServiceProvider().GetRequiredService<IOptions<AsyncMonolithOptions>>().Value;
        configured.AttemptDelay.ShouldBe(TimeSpan.Zero);
        configured.MaxAttempts.ShouldBe(1);
        configured.IdlePollInterval.ShouldBe(TimeSpan.FromMilliseconds(1));
        configured.BusyPollInterval.ShouldBe(TimeSpan.Zero);
        configured.BatchSize.ShouldBe(1);
        configured.DefaultConsumerTimeout.ShouldBe(TimeSpan.FromHours(1));
    }

    [Fact]
    public void Default_settings_are_valid()
    {
        var services = new ServiceCollection();
        services.InternalConfigureAsyncMonolithOptions(_ => { });

        Should.NotThrow(() => services.BuildServiceProvider().GetRequiredService<IOptions<AsyncMonolithOptions>>().Value);
    }

    [Fact]
    public void Consumer_attributes_are_read_at_registration()
    {
        var registry = Registry();

        registry.ResolveConsumerTimeout(nameof(TimeoutConsumer)).ShouldBe(1);
        registry.ResolveConsumerMaxAttempts(MessageFor(nameof(ExceptionConsumer2Attempts))).ShouldBe(2);
        registry.ResolveConsumerExecutionMode(nameof(SequentialProbeConsumer))
            .ShouldBe(ConsumerExecutionMode.Sequential);
        registry.ResolveConsumerRailId(nameof(RailConsumer2)).ShouldBe(2);
    }

    [Fact]
    public void Consumers_without_attributes_use_the_defaults()
    {
        var registry = Registry(s =>
        {
            s.DefaultConsumerTimeout = TimeSpan.FromSeconds(42);
            s.MaxAttempts = 7;
        });

        registry.ResolveConsumerTimeout(nameof(ExceptionConsumer)).ShouldBe(42);
        registry.ResolveConsumerMaxAttempts(MessageFor(nameof(ExceptionConsumer))).ShouldBe(7);
        registry.ResolveConsumerExecutionMode(nameof(ExceptionConsumer))
            .ShouldBe(ConsumerExecutionMode.Parallel);
        registry.ResolveConsumerRailId(nameof(ExceptionConsumer)).ShouldBe(0);
    }

    [Fact]
    public void Non_positive_attribute_values_fall_back_to_the_defaults()
    {
        var registry = Registry(s =>
        {
            s.DefaultConsumerTimeout = TimeSpan.FromSeconds(42);
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
            s.DefaultConsumerTimeout = TimeSpan.FromSeconds(42);
            s.MaxAttempts = 7;
        });

        Should.Throw<InvalidOperationException>(() => registry.ResolvePayloadConsumerTypes("UnknownPayload"));
        Should.Throw<InvalidOperationException>(() => registry.ResolveConsumerType(MessageFor("UnknownConsumer")));
        registry.ResolveConsumerTimeout("UnknownConsumer").ShouldBe(42);
        registry.ResolveConsumerMaxAttempts(MessageFor("UnknownConsumer")).ShouldBe(7);
        registry.ResolveConsumerRailId("UnknownConsumer").ShouldBe(0);
        registry.ResolveConsumerExecutionMode("UnknownConsumer").ShouldBe(ConsumerExecutionMode.Parallel);
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

        services.BuildServiceProvider().GetRequiredService<IOptions<AsyncMonolithOptions>>().Value.MaxAttempts
            .ShouldBe(9);
    }
}
