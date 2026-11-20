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
///     By default all DbContexts share one open in-memory connection, so consumers must not write to the
///     database while a batch is being processed. Pass <c>fileDatabase: true</c> to give every DbContext its
///     own connection to a temporary database file, as in a real host.
/// </remarks>
public sealed class SqliteTestHost : IAsyncDisposable
{
    private readonly SqliteConnection? _connection;
    private readonly string? _path;

    private SqliteTestHost(SqliteConnection? connection, string? path, ServiceProvider services,
        FakeTimeProvider time, TestConsumerInvocations invocations)
    {
        _connection = connection;
        _path = path;
        Services = services;
        Time = time;
        Invocations = invocations;
    }

    public ServiceProvider Services { get; }
    public FakeTimeProvider Time { get; }
    public TestConsumerInvocations Invocations { get; }

    public long Now => Time.GetUtcNow().ToUnixTimeSeconds();

    internal ConsumerMessageProcessor<TestDbContext> ConsumerProcessor =>
        Services.GetRequiredService<ConsumerMessageProcessor<TestDbContext>>();

    internal ScheduledMessageProcessor<TestDbContext> ScheduledProcessor =>
        Services.GetRequiredService<ScheduledMessageProcessor<TestDbContext>>();

    public static async Task<SqliteTestHost> Create(Action<AsyncMonolithOptions>? configure = null,
        bool fileDatabase = false)
    {
        SqliteConnection? connection = null;
        string? path = null;
        if (fileDatabase)
        {
            path = Path.Combine(Path.GetTempPath(), $"asyncmonolith-{Guid.NewGuid():N}.db");
        }
        else
        {
            connection = new SqliteConnection("DataSource=:memory:");
            await connection.OpenAsync();
        }

        var settings = new AsyncMonolithOptions();
        configure?.Invoke(settings);

        var services = new ServiceCollection();
        services.AddDbContext<TestDbContext>(options =>
        {
            if (connection is not null)
            {
                options.UseSqlite(connection);
            }
            else
            {
                // A short busy timeout makes lock contention fail fast instead of waiting 30 seconds.
                options.UseSqlite($"Data Source={path};Default Timeout=3");
            }
        });
        var (time, invocations) = services.AddTestServices(DbType.Ef, settings);
        services.AddSingleton<ConsumerMessageProcessor<TestDbContext>>();
        services.AddSingleton<ScheduledMessageProcessor<TestDbContext>>();
        services.AddSingleton<ConcurrencyProbe>();
        services.AddSingleton<ContextRecorder>();

        var provider = services.BuildServiceProvider();
        using (var scope = provider.CreateScope())
        {
            await scope.ServiceProvider.GetRequiredService<TestDbContext>().Database.EnsureCreatedAsync();
        }

        return new SqliteTestHost(connection, path, provider, time, invocations);
    }

    /// <summary>Produces a message in its own scope and saves it.</summary>
    public async Task Produce<T>(T payload, TimeSpan? delay = null, string? deduplicationKey = null)
        where T : IConsumerPayload
    {
        using var scope = Services.CreateScope();
        await scope.ServiceProvider.GetRequiredService<IProducerService>()
            .Produce(payload, delay, deduplicationKey);
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
        if (_connection is not null)
        {
            await _connection.DisposeAsync();
        }

        if (_path is not null)
        {
            SqliteConnection.ClearAllPools();
            foreach (var file in new[] { _path, _path + "-journal", _path + "-wal", _path + "-shm" })
            {
                File.Delete(file);
            }
        }
    }
}
