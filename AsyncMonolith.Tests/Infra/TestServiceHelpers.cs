using System.Reflection;
using AsyncMonolith.Producers;
using AsyncMonolith.Scheduling;
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
        this ServiceCollection services, AsyncMonolithSettings settings)
    {
        var fakeTime = new FakeTimeProvider(DateTimeOffset.Parse("2020-08-31T10:00:00.0000000Z"));
        services.AddSingleton<TimeProvider>(fakeTime);
        services.AddLogging();

        services.Configure<AsyncMonolithSettings>(options =>
        {
            options.AttemptDelay = settings.AttemptDelay;
            options.MaxAttempts = settings.MaxAttempts;
            options.ProcessorMaxDelay = settings.ProcessorMaxDelay;
            options.ProcessorMinDelay = settings.ProcessorMinDelay;
            options.DbType = settings.DbType;
        });

        services.Register(Assembly.GetExecutingAssembly());
        services.AddSingleton<IAsyncMonolithIdGenerator>(new AsyncMonolithIdGenerator());
        services.AddScoped<ProducerService<TestDbContext>>();
        services.AddScoped<ScheduledMessageService<TestDbContext>>();

        services.AddSingleton<IAsyncMonolithIdGenerator>(new FakeIdGenerator());

        var invocations = new TestConsumerInvocations();
        services.AddSingleton(invocations);

        return (fakeTime, invocations);
    }
}