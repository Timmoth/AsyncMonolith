using System.Reflection;
using AsyncMonolith.Consumers;
using AsyncMonolith.Ef;
using AsyncMonolith.MariaDb;
using AsyncMonolith.MsSql;
using AsyncMonolith.MySql;
using AsyncMonolith.PostgreSql;
using AsyncMonolith.Producers;
using AsyncMonolith.Scheduling;
using AsyncMonolith.TestHelpers;
using AsyncMonolith.Utilities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Time.Testing;

namespace AsyncMonolith.Tests.Infra;

public static class TestServiceHelpers
{
    public static void AddInMemoryDb(this ServiceCollection services)
    {
        var dbId = Guid.NewGuid().ToString();
        services.AddDbContext<TestDbContext>((sp, options) => { options.UseInMemoryDatabase(dbId); }
        );
    }

    public static (FakeTimeProvider fakeTime, TestConsumerInvocations invocations) AddTestServices(
        this ServiceCollection services, DbType dbType, AsyncMonolithSettings settings)
    {
        var fakeTime = new FakeTimeProvider(DateTimeOffset.Parse("2020-08-31T10:00:00.0000000Z"));
        services.AddSingleton<TimeProvider>(fakeTime);
        services.AddLogging();

        settings = services.InternalConfigureAsyncMonolithSettings(settings);
        services.InternalRegisterAsyncMonolithConsumers(Assembly.GetExecutingAssembly(), settings);
        services.AddSingleton<IAsyncMonolithIdGenerator>(new FakeIdGenerator());
        services.AddScoped<IScheduleService, ScheduleService<TestDbContext>>();
        switch (dbType)
        {
            case DbType.Ef:
                services.AddScoped<IProducerService, EfProducerService<TestDbContext>>();
                services.AddSingleton<IConsumerMessageFetcher, EfConsumerMessageFetcher>();
                services.AddSingleton<IScheduledMessageFetcher, EfScheduledMessageFetcher>();
                break;
            case DbType.MySql:
                services.AddScoped<IProducerService, MySqlProducerService<TestDbContext>>();
                services.AddSingleton<IConsumerMessageFetcher, MySqlConsumerMessageFetcher>();
                services.AddSingleton<IScheduledMessageFetcher, MySqlScheduledMessageFetcher>();
                break;
            case DbType.MsSql:
                services.AddScoped<IProducerService, MsSqlProducerService<TestDbContext>>();
                services.AddSingleton<IConsumerMessageFetcher, MsSqlConsumerMessageFetcher>();
                services.AddSingleton<IScheduledMessageFetcher, MsSqlScheduledMessageFetcher>();
                break;
            case DbType.PostgreSql:
                services.AddScoped<IProducerService, PostgreSqlProducerService<TestDbContext>>();
                services.AddSingleton<IConsumerMessageFetcher, PostgreSqlConsumerMessageFetcher>();
                services.AddSingleton<IScheduledMessageFetcher, PostgreSqlScheduledMessageFetcher>();
                break;
            case DbType.MariaDb:
                services.AddScoped<IProducerService, MariaDbProducerService<TestDbContext>>();
                services.AddSingleton<IConsumerMessageFetcher, MariaDbConsumerMessageFetcher>();
                services.AddSingleton<IScheduledMessageFetcher, MariaDbScheduledMessageFetcher>();
                break;
        }

        var invocations = new TestConsumerInvocations();
        services.AddSingleton(invocations);

        return (fakeTime, invocations);
    }
}