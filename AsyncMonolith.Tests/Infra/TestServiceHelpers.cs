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
        services.AddDbContext<TestDbContext>((_, options) => { options.UseInMemoryDatabase(dbId); }
        );
    }

    public static (FakeTimeProvider fakeTime, TestConsumerInvocations invocations) AddTestServices(
        this ServiceCollection services, DbType dbType, AsyncMonolithSettings settings)
    {
        var fakeTime = new FakeTimeProvider(DateTimeOffset.Parse("2020-08-31T10:00:00.0000000Z", System.Globalization.CultureInfo.InvariantCulture));
        services.AddSingleton<TimeProvider>(fakeTime);
        services.AddLogging();

        services.InternalConfigureAsyncMonolithSettings(settings);
        settings.RegisterTypesFromAssembly(Assembly.GetExecutingAssembly());
        
        
        services.InternalRegisterAsyncMonolithConsumers(settings);
        services.AddSingleton<IAsyncMonolithIdGenerator>(new FakeIdGenerator());
        services.AddScoped<IProducerService, ProducerService<TestDbContext>>();
        services.AddScoped<IScheduleService, ScheduleService<TestDbContext>>();
        services.AddScoped<IPoisonedMessageService, PoisonedMessageService<TestDbContext>>();
        services.AddScoped<IQueueStatisticsService, QueueStatisticsService<TestDbContext>>();
        switch (dbType)
        {
            case DbType.Ef:
                services.AddSingleton<IConsumerMessageFetcher, EfConsumerMessageFetcher>();
                services.AddSingleton<IScheduledMessageFetcher, EfScheduledMessageFetcher>();
                break;
            case DbType.MySql:
                services.AddSingleton<IConsumerMessageFetcher, MySqlConsumerMessageFetcher>();
                services.AddSingleton<IScheduledMessageFetcher, MySqlScheduledMessageFetcher>();
                break;
            case DbType.MsSql:
                services.AddSingleton<IConsumerMessageFetcher, MsSqlConsumerMessageFetcher>();
                services.AddSingleton<IScheduledMessageFetcher, MsSqlScheduledMessageFetcher>();
                break;
            case DbType.PostgreSql:
                services.AddSingleton<IConsumerMessageFetcher, PostgreSqlConsumerMessageFetcher>();
                services.AddSingleton<IScheduledMessageFetcher, PostgreSqlScheduledMessageFetcher>();
                break;
            case DbType.MariaDb:
                services.AddSingleton<IConsumerMessageFetcher, MariaDbConsumerMessageFetcher>();
                services.AddSingleton<IScheduledMessageFetcher, MariaDbScheduledMessageFetcher>();
                break;
        }

        var invocations = new TestConsumerInvocations();
        services.AddSingleton(invocations);

        return (fakeTime, invocations);
    }
}