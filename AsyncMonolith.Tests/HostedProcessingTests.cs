using AsyncMonolith.Consumers;
using AsyncMonolith.Ef;
using AsyncMonolith.Producers;
using AsyncMonolith.Scheduling;
using AsyncMonolith.Tests.Infra;
using AsyncMonolith.Utilities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Shouldly;

namespace AsyncMonolith.Tests;

/// <summary>
///     End to end through a real generic host: AddEfAsyncMonolith, the background processors (one per rail plus
///     the scheduler), the real clock and a SQLite database file. No Docker, so these run with the fast tests.
/// </summary>
public sealed class HostedProcessingTests : IAsyncLifetime
{
    private static readonly TimeSpan WaitLimit = TimeSpan.FromSeconds(10);
    private readonly string _path = Path.Combine(Path.GetTempPath(), $"asyncmonolith-host-{Guid.NewGuid():N}.db");
    private IHost _host = default!;

    private TestConsumerInvocations Invocations => _host.Services.GetRequiredService<TestConsumerInvocations>();

    public ValueTask InitializeAsync() => ValueTask.CompletedTask;

    private async Task StartHost(Action<AsyncMonolithSettings>? configure = null)
    {
        var builder = Host.CreateApplicationBuilder();
        builder.Services.AddDbContext<TestDbContext>(o => o.UseSqlite($"Data Source={_path}"));
        builder.Services.AddSingleton(TimeProvider.System);
        builder.Services.AddSingleton<TestConsumerInvocations>();
        builder.Services.AddSingleton<ConcurrencyProbe>();
        builder.Services.AddEfAsyncMonolith<TestDbContext>(s =>
        {
            s.RegisterTypesFromAssemblyContaining<HostedProcessingTests>();
            s.ProcessorMaxDelay = 50;
            s.ProcessorMinDelay = 10;
            configure?.Invoke(s);
        });
        _host = builder.Build();

        using (var scope = _host.Services.CreateScope())
        {
            await scope.ServiceProvider.GetRequiredService<TestDbContext>().Database.EnsureCreatedAsync();
        }

        await _host.StartAsync(TestContext.Current.CancellationToken);
    }

    private async Task Produce<T>(T payload) where T : IConsumerPayload
    {
        using var scope = _host.Services.CreateScope();
        await scope.ServiceProvider.GetRequiredService<IProducerService>()
            .Produce(payload, cancellationToken: TestContext.Current.CancellationToken);
        await scope.ServiceProvider.GetRequiredService<TestDbContext>()
            .SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    private async Task<T> Query<T>(Func<TestDbContext, Task<T>> query)
    {
        using var scope = _host.Services.CreateScope();
        return await query(scope.ServiceProvider.GetRequiredService<TestDbContext>());
    }

    private static async Task WaitUntil(Func<Task<bool>> condition, string what)
    {
        var deadline = DateTime.UtcNow + WaitLimit;
        while (!await condition())
        {
            if (DateTime.UtcNow > deadline)
            {
                throw new TimeoutException($"Timed out waiting until {what}");
            }

            await Task.Delay(20, TestContext.Current.CancellationToken);
        }
    }

    [Fact]
    public async Task Produced_message_is_consumed_by_the_running_host()
    {
        await StartHost();

        await Produce(new SingleConsumerMessage { Name = "hello" });

        await WaitUntil(() => Query(db => db.ConsumerMessages.AnyAsync()).ContinueWith(t => !t.Result),
            "the message is consumed");
        Invocations.GetInvocationCount(nameof(SingleConsumer)).ShouldBe(1);
    }

    [Fact]
    public async Task Consumer_can_write_to_the_database_and_chain_another_message()
    {
        await StartHost();

        await Produce(new WritingConsumerMessage { Name = "chained" });

        await WaitUntil(() => Task.FromResult(Invocations.GetInvocationCount(nameof(SingleConsumer)) == 1),
            "the chained message is consumed");
        (await Query(db => db.PoisonedMessages.CountAsync())).ShouldBe(0);
        await WaitUntil(() => Query(db => db.ConsumerMessages.AnyAsync()).ContinueWith(t => !t.Result),
            "the queue is empty");
    }

    [Fact]
    public async Task Messages_on_every_rail_are_consumed()
    {
        await StartHost();

        await Produce(new RailConsumerMessage());

        await WaitUntil(() => Task.FromResult(
                Invocations.GetInvocationCount(nameof(RailConsumer0)) == 1 &&
                Invocations.GetInvocationCount(nameof(RailConsumer1)) == 1 &&
                Invocations.GetInvocationCount(nameof(RailConsumer2)) == 1),
            "a consumer on each rail has run");
    }

    [Fact]
    public async Task Failing_message_is_poisoned_by_the_running_host()
    {
        await StartHost(s =>
        {
            s.MaxAttempts = 2;
            s.AttemptDelay = 0;
        });

        await Produce(new ExceptionConsumerMessage { Name = "boom" });

        await WaitUntil(() => Query(db => db.PoisonedMessages.AnyAsync()), "the message is poisoned");
        Invocations.GetInvocationCount(nameof(ExceptionConsumer)).ShouldBe(2);
        (await Query(db => db.ConsumerMessages.CountAsync())).ShouldBe(0);
    }

    [Fact]
    public async Task Scheduled_message_fires_repeatedly_on_the_running_host()
    {
        await StartHost();

        using (var scope = _host.Services.CreateScope())
        {
            scope.ServiceProvider.GetRequiredService<IScheduleService>()
                .Schedule(new SingleConsumerMessage { Name = "tick" }, "* * * * * *", "UTC");
            await scope.ServiceProvider.GetRequiredService<TestDbContext>()
                .SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        await WaitUntil(() => Task.FromResult(Invocations.GetInvocationCount(nameof(SingleConsumer)) >= 2),
            "the schedule has fired twice");
    }

    public async ValueTask DisposeAsync()
    {
        if (_host is not null)
        {
            await _host.StopAsync(CancellationToken.None);
            _host.Dispose();
        }

        Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
        foreach (var file in new[] { _path, _path + "-journal", _path + "-wal", _path + "-shm" })
        {
            if (File.Exists(file))
            {
                File.Delete(file);
            }
        }
    }
}
