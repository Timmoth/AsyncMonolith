using AsyncMonolith.Utilities;
using AsyncMonolith.Consumers;
using AsyncMonolith.Producers;
using AsyncMonolith.Scheduling;
using AsyncMonolith.Tests.Infra;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;

namespace AsyncMonolith.Tests;

/// <summary>
///     ConfigureAsyncMonolith maps the tables itself, with an optional schema and prefix.
/// </summary>
public class ModelMappingTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Default_table_names_are_unchanged()
    {
        await using var connection = new SqliteConnection("DataSource=:memory:");
        await using var db = new TestDbContext(new DbContextOptionsBuilder<TestDbContext>().UseSqlite(connection).Options);

        db.Model.FindEntityType(typeof(ConsumerMessage))!.GetTableName().ShouldBe("consumer_messages");
        db.Model.FindEntityType(typeof(PoisonedMessage))!.GetTableName().ShouldBe("poisoned_messages");
        db.Model.FindEntityType(typeof(ScheduledMessage))!.GetTableName().ShouldBe("scheduled_messages");
    }

    /// <summary>Runs produce, consume, poison and schedule against a host DbContext type.</summary>
    internal static async Task RunEndToEnd<TDb>(IServiceProvider services) where TDb : DbContext
    {
        using (var scope = services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<TDb>();
            var producer = scope.ServiceProvider.GetRequiredService<IProducerService>();
            await producer.Produce(new SingleConsumerMessage { Name = "ok" }, cancellationToken: Ct);
            await producer.Produce(new MixedOutcomeMessage(), cancellationToken: Ct);
            await scope.ServiceProvider.GetRequiredService<IScheduleService>()
                .EnsureSchedule(new SingleConsumerMessage { Name = "tick" }, "* * * * * *", "UTC", "tick", Ct);
            await db.SaveChangesAsync(Ct);
        }

        var processor = ActivatorUtilities.CreateInstance<ConsumerMessageProcessor<TDb>>(services);
        (await processor.ProcessBatch(Ct)).ShouldBe(2);

        using (var scope = services.CreateScope())
        {
            var poisoned = scope.ServiceProvider.GetRequiredService<IPoisonedMessageService>();
            var statistics = await scope.ServiceProvider.GetRequiredService<IQueueStatisticsService>().Read(Ct);
            statistics.Schedules.ShouldBe(1);
            statistics.Waiting.ShouldBe(1); // MixedFailureConsumer, waiting to retry
            (await poisoned.Count(Ct)).ShouldBe(0);
        }
    }

    private static ServiceProvider Services<TDb>(Action<DbContextOptionsBuilder> configureDb,
        Action<IServiceCollection> addAsyncMonolith) where TDb : DbContext
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton<TestConsumerInvocations>();
        services.AddSingleton<ConcurrencyProbe>();
        services.AddSingleton<ContextRecorder>();
        services.AddDbContext<TDb>(configureDb);
        addAsyncMonolith(services);
        return services.BuildServiceProvider();
    }

    [Fact]
    public async Task A_host_context_without_DbSets_can_use_prefixed_tables()
    {
        var path = Path.Combine(Path.GetTempPath(), $"asyncmonolith-prefix-{Guid.NewGuid():N}.db");
        try
        {
            await using var services = Services<PrefixedDbContext>(o => o.UseSqlite($"Data Source={path}"),
                s => s.AddAsyncMonolith<PrefixedDbContext>(o => o.RegisterTypesFromAssemblyContaining<ModelMappingTests>()));
            using (var scope = services.CreateScope())
            {
                await scope.ServiceProvider.GetRequiredService<PrefixedDbContext>().Database.EnsureCreatedAsync(Ct);
            }

            await RunEndToEnd<PrefixedDbContext>(services);

            await using var connection = new SqliteConnection($"Data Source={path}");
            await connection.OpenAsync(Ct);
            await using var command = connection.CreateCommand();
            command.CommandText = "SELECT name FROM sqlite_master WHERE type = 'table' ORDER BY name";
            var tables = new List<string>();
            await using (var reader = await command.ExecuteReaderAsync(Ct))
            {
                while (await reader.ReadAsync(Ct))
                {
                    tables.Add(reader.GetString(0));
                }
            }

            tables.ShouldBe(["jobs_consumer_messages", "jobs_poisoned_messages", "jobs_scheduled_messages"]);
        }
        finally
        {
            SqliteConnection.ClearAllPools();
            File.Delete(path);
        }
    }

    [Theory]
    [Trait("Category", "Integration")]
    [InlineData(DbType.PostgreSql)]
    [InlineData(DbType.MsSql)]
    public async Task Provider_sql_finds_tables_in_a_custom_schema(DbType dbType)
    {
        var container = DbTestsBase.GetTestDbContainer(dbType);
        await container.InitializeAsync();
        try
        {
            await using var services = Services<SchemaDbContext>(
                o =>
                {
                    if (dbType == DbType.PostgreSql)
                    {
                        o.UseNpgsql(container.ConnectionString);
                    }
                    else
                    {
                        o.UseSqlServer(container.ConnectionString);
                    }
                },
                s => s.AddAsyncMonolith<SchemaDbContext>(o => o.RegisterTypesFromAssemblyContaining<ModelMappingTests>()));
            using (var scope = services.CreateScope())
            {
                await scope.ServiceProvider.GetRequiredService<SchemaDbContext>().Database.EnsureCreatedAsync(Ct);
            }

            await RunEndToEnd<SchemaDbContext>(services);
        }
        finally
        {
            await container.DisposeAsync();
        }
    }
}
