using System.Reflection;
using AsnyMonolith.Producers;
using AsnyMonolith.Scheduling;
using AsnyMonolith.Utilities;
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
        this ServiceCollection services)
    {
        var fakeTime = new FakeTimeProvider(DateTimeOffset.Parse("2020-08-31T10:00:00.0000000Z"));
        services.AddSingleton<TimeProvider>(fakeTime);
        services.AddLogging();

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