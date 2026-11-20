using AsyncMonolith.Ef;
using AsyncMonolith.MariaDb;
using AsyncMonolith.MsSql;
using AsyncMonolith.MySql;
using AsyncMonolith.PostgreSql;
using AsyncMonolith.Producers;
using AsyncMonolith.Tests.Infra;
using AsyncMonolith.Utilities;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Shouldly;

namespace AsyncMonolith.Tests;

/// <summary>
///     Several app instances sharing one real database under load.
/// </summary>
[Trait("Category", "Integration")]
public class LoadTests
{
    private const int Instances = 3;
    private const int Messages = 1000;

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static void AddAsyncMonolith(IServiceCollection services, DbType dbType)
    {
        Action<AsyncMonolithOptions> configure = o =>
        {
            o.RegisterTypesFromAssemblyContaining<LoadTests>();
            o.BatchSize = 20;
            o.AttemptDelay = TimeSpan.Zero;
            o.IdlePollInterval = TimeSpan.FromMilliseconds(50);
            o.BusyPollInterval = TimeSpan.Zero;
        };
        _ = dbType switch
        {
            DbType.Ef => services.AddEfAsyncMonolith<TestDbContext>(configure),
            DbType.PostgreSql => services.AddPostgreSqlAsyncMonolith<TestDbContext>(configure),
            DbType.MsSql => services.AddMsSqlAsyncMonolith<TestDbContext>(configure),
            DbType.MySql => services.AddMySqlAsyncMonolith<TestDbContext>(configure),
            DbType.MariaDb => services.AddMariaDbAsyncMonolith<TestDbContext>(configure),
            _ => throw new ArgumentOutOfRangeException(nameof(dbType))
        };
    }

    [Theory]
    [MemberData(nameof(DbTestsBase.GetTestDbTypesWithoutEf), MemberType = typeof(DbTestsBase))]
    public async Task Several_instances_consume_every_message_exactly_once(DbType dbType)
    {
        var container = DbTestsBase.GetTestDbContainer(dbType);
        await container.InitializeAsync();
        var recorder = new LoadRecorder();
        var hosts = new List<IHost>();
        try
        {
            for (var i = 0; i < Instances; i++)
            {
                var builder = Host.CreateApplicationBuilder();
                container.AddDb(builder.Services);
                builder.Services.AddSingleton(recorder);
                builder.Services.AddSingleton<TestConsumerInvocations>();
                builder.Services.AddSingleton<ConcurrencyProbe>();
                builder.Services.AddSingleton<ContextRecorder>();
                builder.Services.AddSingleton<SerializationRecorder>();
                AddAsyncMonolith(builder.Services, dbType);
                hosts.Add(builder.Build());
            }

            using (var scope = hosts[0].Services.CreateScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<TestDbContext>();
                await db.Database.EnsureCreatedAsync(Ct);
                await scope.ServiceProvider.GetRequiredService<IProducerService>().ProduceList(
                    Enumerable.Range(0, Messages).Select(n => new LoadMessage { Number = n }),
                    cancellationToken: Ct);
                await db.SaveChangesAsync(Ct);
            }

            foreach (var host in hosts)
            {
                await host.StartAsync(Ct);
            }

            var deadline = DateTime.UtcNow.AddMinutes(2);
            while (recorder.Successes.Count < Messages && DateTime.UtcNow < deadline)
            {
                await Task.Delay(100, Ct);
            }

            // Give any duplicate a chance to appear before checking.
            await Task.Delay(500, Ct);

            recorder.Successes.Count.ShouldBe(Messages);
            recorder.Successes.Values.ShouldAllBe(n => n == 1, "no message succeeds twice");
            recorder.Attempts.Where(a => a.Key % 10 == 0).ShouldAllBe(a => a.Value == 2,
                "every tenth message fails once and is retried once");
            recorder.Attempts.Where(a => a.Key % 10 != 0).ShouldAllBe(a => a.Value == 1);

            using (var scope = hosts[0].Services.CreateScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<TestDbContext>();
                db.ConsumerMessages.Count().ShouldBe(0);
                db.PoisonedMessages.Count().ShouldBe(0);
            }
        }
        finally
        {
            foreach (var host in hosts)
            {
                await host.StopAsync(CancellationToken.None);
                host.Dispose();
            }

            await container.DisposeAsync();
        }
    }
}
