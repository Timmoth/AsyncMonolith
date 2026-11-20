using AsyncMonolith.Database;
using Microsoft.Extensions.DependencyInjection.Extensions;
using System.Diagnostics.Metrics;
using System.Reflection;
using AsyncMonolith.Consumers;
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
        this ServiceCollection services, DbType dbType, AsyncMonolithOptions settings)
    {
        var fakeTime = new FakeTimeProvider(DateTimeOffset.Parse("2020-08-31T10:00:00.0000000Z", System.Globalization.CultureInfo.InvariantCulture));
        services.AddSingleton<TimeProvider>(fakeTime);
        services.AddLogging();
        services.TryAddSingleton(sp => new AsyncMonolithMetrics(sp.GetService<IMeterFactory>()));

        services.InternalConfigureAsyncMonolithOptions(o =>
        {
            o.MaxAttempts = settings.MaxAttempts;
            o.AttemptDelay = settings.AttemptDelay;
            o.MaxAttemptDelay = settings.MaxAttemptDelay;
            o.DefaultConsumerTimeout = settings.DefaultConsumerTimeout;
            o.BatchSize = settings.BatchSize;
            o.IdlePollInterval = settings.IdlePollInterval;
            o.BusyPollInterval = settings.BusyPollInterval;
            o.SerializerOptions = settings.SerializerOptions;
            o.PoisonedMessageRetention = settings.PoisonedMessageRetention;
        });
        settings.RegisterTypesFromAssembly(Assembly.GetExecutingAssembly());
        
        
        services.InternalRegisterAsyncMonolithConsumers(settings);
        services.AddSingleton<IAsyncMonolithIdGenerator>(new FakeIdGenerator());
        services.AddScoped<ProducerService<TestDbContext>>();
        services.AddScoped<IProducerService, ProducerService<TestDbContext>>();
        services.AddScoped<IScheduleService, ScheduleService<TestDbContext>>();
        services.AddScoped<IPoisonedMessageService, PoisonedMessageService<TestDbContext>>();
        services.AddScoped<IQueueStatisticsService, QueueStatisticsService<TestDbContext>>();
        // One fetcher pair for every database: the SQL is chosen from the EF provider in use.
        services.AddSingleton<IConsumerMessageFetcher, ConsumerMessageFetcher>();
        services.AddSingleton<IScheduledMessageFetcher, ScheduledMessageFetcher>();

        var invocations = new TestConsumerInvocations();
        services.AddSingleton(invocations);

        return (fakeTime, invocations);
    }
}