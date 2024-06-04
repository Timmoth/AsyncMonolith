using AsyncMonolith.Consumers;
using AsyncMonolith.Scheduling;
using AsyncMonolith.Utilities;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Time.Testing;

namespace AsyncMonolith.Tests.Infra;

public abstract class DbTestsBase
{
    protected FakeTimeProvider FakeTime = default!;
    protected TestConsumerInvocations TestConsumerInvocations = default!;

    protected async Task<ServiceProvider> Setup(TestDbContainerBase dbContainer)
    {
        await dbContainer.InitializeAsync();

        var services = new ServiceCollection();

        dbContainer.AddDb(services);

        var (fakeTime, invocations) = services.AddTestServices(new AsyncMonolithSettings
        {
            DbType = dbContainer.DbType
        });
        TestConsumerInvocations = invocations;
        FakeTime = fakeTime;

        services.AddSingleton<ScheduledMessageProcessor<TestDbContext>>();
        services.AddSingleton<ConsumerMessageProcessor<TestDbContext>>();

        var serviceProvider = services.BuildServiceProvider();

        using var scope = serviceProvider.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<TestDbContext>();
        await dbContext.Database.EnsureCreatedAsync();

        await dbContext.SaveChangesAsync();

        return serviceProvider;
    }


    public static IEnumerable<object[]> GetTestDbContainers()
    {
        yield return new object[] { new MySqlTestDbContainer() };
        yield return new object[] { new PostgreSqlTestDbContainer() };
        yield return new object[] { new EfTestDbContainer() };
    }
}