using AsyncMonolith.Consumers;
using AsyncMonolith.Scheduling;
using AsyncMonolith.Utilities;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Time.Testing;
using System.Diagnostics;

namespace AsyncMonolith.Tests.Infra;

public abstract class DbTestsBase
{
    protected FakeTimeProvider FakeTime = default!;
    protected TestConsumerInvocations TestConsumerInvocations = default!;

    protected async Task<ServiceProvider> Setup(TestDbContainerBase dbContainer, AsyncMonolithSettings? settings = null)
    {
        await dbContainer.InitializeAsync();

        var services = new ServiceCollection();

        dbContainer.AddDb(services);

        var (fakeTime, invocations) =
            services.AddTestServices(dbContainer.DbType, settings ?? AsyncMonolithSettings.Default);
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

    public Activity? GetActivity()
    {
        var activitySource = new ActivitySource("AsyncMonolith.Tests");
        var listener = new ActivityListener
        {
            ShouldListenTo = (a) => a.Name == activitySource.Name,
            Sample = (ref ActivityCreationOptions<ActivityContext> options) => ActivitySamplingResult.AllDataAndRecorded,
            ActivityStarted = activity => Console.WriteLine($"Activity started: {activity.DisplayName}"),
            ActivityStopped = activity => Console.WriteLine($"Activity stopped: {activity.DisplayName}")
        };
        ActivitySource.AddActivityListener(listener);
        return activitySource.StartActivity(
            "TestActivity",
            ActivityKind.Internal
        );
    }
    public static IEnumerable<object[]> GetTestDbContainers()
    {
        yield return new object[] { new MySqlTestDbContainer() };
        yield return new object[] { new MsSqlTestDbContainer() };
        yield return new object[] { new PostgreSqlTestDbContainer() };
        yield return new object[] { new EfTestDbContainer() };
        yield return new object[] { new MariaDbTestDbContainer() };
    }

    public static TestDbContainerBase GetTestDbContainer(DbType dbType)
    {
        return dbType switch
        {
            DbType.Ef => new EfTestDbContainer(),
            DbType.MySql => new MySqlTestDbContainer(),
            DbType.MsSql => new MsSqlTestDbContainer(),
            DbType.PostgreSql => new PostgreSqlTestDbContainer(),
            DbType.MariaDb => new MariaDbTestDbContainer(),
            _ => throw new ArgumentOutOfRangeException(nameof(dbType), dbType, null)
        };


    }
}