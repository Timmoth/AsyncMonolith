using System.Collections.Concurrent;
using System.Diagnostics.Metrics;
using AsyncMonolith.Ef;
using AsyncMonolith.Tests.Infra;
using AsyncMonolith.Utilities;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;

namespace AsyncMonolith.Tests;

/// <summary>
///     The metrics published on the async_monolith meter.
/// </summary>
public sealed class MetricsTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    /// <summary>Records measurements from one host's meter only.</summary>
    private sealed class Recorder : IDisposable
    {
        private readonly MeterListener _listener = new();

        public Recorder(Meter meter)
        {
            _listener.InstrumentPublished = (instrument, listener) =>
            {
                if (instrument.Meter == meter)
                {
                    listener.EnableMeasurementEvents(instrument);
                }
            };
            _listener.SetMeasurementEventCallback<long>((i, value, tags, _) => Add(i, value, tags));
            _listener.SetMeasurementEventCallback<double>((i, value, tags, _) => Add(i, value, tags));
            _listener.Start();
        }

        public ConcurrentQueue<(string Name, double Value, Dictionary<string, object?> Tags)> Measurements { get; } = new();

        private void Add(Instrument instrument, double value, ReadOnlySpan<KeyValuePair<string, object?>> tags) =>
            Measurements.Enqueue((instrument.Name, value, tags.ToArray().ToDictionary(t => t.Key, t => t.Value)));

        public double Sum(string name, string? tag = null, object? tagValue = null) => Measurements
            .Where(m => m.Name == name && (tag is null || Equals(m.Tags.GetValueOrDefault(tag), tagValue)))
            .Sum(m => m.Value);

        public int Count(string name) => Measurements.Count(m => m.Name == name);

        public void Dispose() => _listener.Dispose();
    }

    private static Recorder Listen(SqliteTestHost host) =>
        new(host.Services.GetRequiredService<AsyncMonolithMetrics>().Meter);

    [Fact]
    public async Task Producing_counts_a_message_per_consumer()
    {
        await using var host = await SqliteTestHost.Create();
        using var recorder = Listen(host);

        await host.Produce(new MultiConsumerMessage { Name = "x" });

        recorder.Sum("async_monolith.messages.produced").ShouldBe(2);
        recorder.Sum("async_monolith.messages.produced", "consumer_type", nameof(MultiConsumer1)).ShouldBe(1);
        recorder.Sum("async_monolith.messages.produced", "payload_type", nameof(MultiConsumerMessage)).ShouldBe(2);
    }

    [Fact]
    public async Task Consuming_records_outcomes_durations_and_lag()
    {
        await using var host = await SqliteTestHost.Create();
        using var recorder = Listen(host);
        await host.Produce(new SingleConsumerMessage { Name = "a" });
        await host.Produce(new MixedOutcomeMessage());
        host.Time.Advance(TimeSpan.FromSeconds(5));

        await host.ConsumerProcessor.ProcessBatch(Ct);

        recorder.Sum("async_monolith.messages.consumed").ShouldBe(2);
        recorder.Sum("async_monolith.messages.failed", "consumer_type", nameof(MixedFailureConsumer)).ShouldBe(1);
        recorder.Sum("async_monolith.messages.poisoned").ShouldBe(0);
        recorder.Count("async_monolith.consume.duration").ShouldBe(3);
        recorder.Measurements.Where(m => m.Name == "async_monolith.messages.lag").ShouldAllBe(m => m.Value == 5);
    }

    [Fact]
    public async Task Poisoning_is_counted()
    {
        await using var host = await SqliteTestHost.Create(s => s.MaxAttempts = 1);
        using var recorder = Listen(host);
        await host.Produce(new ExceptionConsumerMessage { Name = "boom" });

        await host.ConsumerProcessor.ProcessBatch(Ct);

        recorder.Sum("async_monolith.messages.poisoned", "consumer_type", nameof(ExceptionConsumer)).ShouldBe(1);
        recorder.Measurements.Single(m => m.Name == "async_monolith.consume.duration").Tags["outcome"]
            .ShouldBe("poisoned");
    }

    [Fact]
    public async Task Fired_schedules_are_counted()
    {
        await using var host = await SqliteTestHost.Create();
        using var recorder = Listen(host);
        await host.Schedule(new SingleConsumerMessage { Name = "tick" }, "0 * * * * *");
        host.Time.Advance(TimeSpan.FromMinutes(1));

        await host.ScheduledProcessor.ProcessBatch(Ct);

        recorder.Sum("async_monolith.schedules.fired", "payload_type", nameof(SingleConsumerMessage)).ShouldBe(1);
    }

    [Fact]
    public void The_meter_comes_from_the_hosts_meter_factory_when_there_is_one()
    {
        var services = new ServiceCollection();
        services.AddMetrics();
        services.AddDbContext<TestDbContext>(_ => { });
        services.AddEfAsyncMonolith<TestDbContext>();

        using var provider = services.BuildServiceProvider();
        var meter = provider.GetRequiredService<AsyncMonolithMetrics>().Meter;

        meter.Name.ShouldBe(AsyncMonolithInstrumentation.MeterName);
        meter.Scope.ShouldBe(provider.GetRequiredService<IMeterFactory>());
    }
}
