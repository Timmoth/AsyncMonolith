using AsyncMonolith.Consumers;
using AsyncMonolith.Producers;
using AsyncMonolith.Scheduling;
using AsyncMonolith.Utilities;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Time.Testing;

namespace AsyncMonolith.Tests.Infra;

/// <summary>
///     A fast, in-process host for low-level tests: the real AsyncMonolith services and EF providers
///     over an in-memory SQLite database (relational, with transactions and unique indexes), a fake clock,
///     and no Docker.
/// </summary>
/// <remarks>
///     All DbContexts share one open connection, so consumers used with this host must not write to the
///     database while a batch is being processed.
/// </remarks>
public sealed class SqliteTestHost : IAsyncDisposable
{
    private readonly SqliteConnection _connection;

    private SqliteTestHost(SqliteConnection connection, ServiceProvider services, FakeTimeProvider time,
        TestConsumerInvocations invocations)
    {
        _connection = connection;
        Services = services;
        Time = time;
        Invocations = invocations;
    }

    public ServiceProvider Services { get; }
    public FakeTimeProvider Time { get; }
    public TestConsumerInvocations Invocations { get; }

    public long Now => Time.GetUtcNow().ToUnixTimeSeconds();

    public ConsumerMessageProcessor<TestDbContext> ConsumerProcessor =>
        Services.GetRequiredService<ConsumerMessageProcessor<TestDbContext>>();

    public ScheduledMessageProcessor<TestDbContext> ScheduledProcessor =>
        Services.GetRequiredService<ScheduledMessageProcessor<TestDbContext>>();

    public static async Task<SqliteTestHost> Create(Action<AsyncMonolithSettings>? configure = null)
    {
        var connection = new SqliteConnection("DataSource=:memory:");
        await connection.OpenAsync();

        var settings = AsyncMonolithSettings.Default;
        configure?.Invoke(settings);

        var services = new ServiceCollection();
        services.AddDbContext<TestDbContext>(options => options.UseSqlite(connection));
        var (time, invocations) = services.AddTestServices(DbType.Ef, settings);
        services.AddSingleton<ConsumerMessageProcessor<TestDbContext>>();
        services.AddSingleton<ScheduledMessageProcessor<TestDbContext>>();
        services.AddSingleton<ConcurrencyProbe>();

        var provider = services.BuildServiceProvider();
        using (var scope = provider.CreateScope())
        {
            await scope.ServiceProvider.GetRequiredService<TestDbContext>().Database.EnsureCreatedAsync();
        }

        return new SqliteTestHost(connection, provider, time, invocations);
    }

    /// <summary>Produces a message in its own scope and saves it.</summary>
    public async Task Produce<T>(T payload, long? availableAfter = null, string? insertId = null)
        where T : IConsumerPayload
    {
        using var scope = Services.CreateScope();
        await scope.ServiceProvider.GetRequiredService<IProducerService>()
            .Produce(payload, availableAfter, insertId);
        await scope.ServiceProvider.GetRequiredService<TestDbContext>().SaveChangesAsync();
    }

    /// <summary>Schedules a message in its own scope and saves it.</summary>
    public async Task<string> Schedule<T>(T payload, string cron, string timezone = "UTC", string? tag = null)
        where T : IConsumerPayload
    {
        using var scope = Services.CreateScope();
        var id = scope.ServiceProvider.GetRequiredService<IScheduleService>().Schedule(payload, cron, timezone, tag);
        await scope.ServiceProvider.GetRequiredService<TestDbContext>().SaveChangesAsync();
        return id;
    }

    /// <summary>Runs a query against a fresh DbContext.</summary>
    public async Task<TResult> Query<TResult>(Func<TestDbContext, Task<TResult>> query)
    {
        using var scope = Services.CreateScope();
        return await query(scope.ServiceProvider.GetRequiredService<TestDbContext>());
    }

    /// <summary>Changes data with a fresh DbContext and saves it.</summary>
    public async Task Change(Action<TestDbContext> change)
    {
        using var scope = Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<TestDbContext>();
        change(db);
        await db.SaveChangesAsync();
    }

    public Task<List<ConsumerMessage>> ConsumerMessages() =>
        Query(db => db.ConsumerMessages.OrderBy(m => m.Id).ToListAsync());

    public Task<List<PoisonedMessage>> PoisonedMessages() =>
        Query(db => db.PoisonedMessages.OrderBy(m => m.Id).ToListAsync());

    public Task<List<ScheduledMessage>> ScheduledMessages() =>
        Query(db => db.ScheduledMessages.OrderBy(m => m.Id).ToListAsync());

    public async ValueTask DisposeAsync()
    {
        await Services.DisposeAsync();
        await _connection.DisposeAsync();
    }
}
