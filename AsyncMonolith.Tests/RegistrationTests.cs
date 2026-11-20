using AsyncMonolith.Scheduling;
using AsyncMonolith.TestHelpers;
using AsyncMonolith.Tests.Infra;
using AsyncMonolith.Utilities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Shouldly;

namespace AsyncMonolith.Tests;

/// <summary>
///     What Add*AsyncMonolith registers, and what it leaves to the host.
/// </summary>
public class RegistrationTests
{
    private static ServiceCollection Services()
    {
        var services = new ServiceCollection();
        services.AddSingleton<TestConsumerInvocations>();
        services.AddDbContext<TestDbContext>(_ => { });
        return services;
    }

    private static void AddAsyncMonolith(IServiceCollection services) =>
        services.AddAsyncMonolith<TestDbContext>(s => s.RegisterTypesFromAssemblyContaining<RegistrationTests>());

    [Fact]
    public void Does_not_register_the_DbContext_base_type()
    {
        var services = Services();

        AddAsyncMonolith(services);

        services.ShouldNotContain(d => d.ServiceType == typeof(DbContext));
    }

    [Fact]
    public void Uses_the_system_clock_unless_the_host_registers_one()
    {
        var services = Services();
        AddAsyncMonolith(services);
        services.BuildServiceProvider().GetRequiredService<TimeProvider>().ShouldBe(TimeProvider.System);

        var custom = new Microsoft.Extensions.Time.Testing.FakeTimeProvider();
        var withClock = Services();
        withClock.AddSingleton<TimeProvider>(custom);
        AddAsyncMonolith(withClock);
        withClock.BuildServiceProvider().GetRequiredService<TimeProvider>().ShouldBe(custom);
    }

    [Fact]
    public void Services_registered_by_the_host_first_are_kept()
    {
        var services = Services();
        var ids = new FakeIdGenerator();
        services.AddSingleton<IAsyncMonolithIdGenerator>(ids);
        services.AddScoped<IScheduleService, FakeScheduleService>();

        AddAsyncMonolith(services);

        var provider = services.BuildServiceProvider();
        provider.GetRequiredService<IAsyncMonolithIdGenerator>().ShouldBeSameAs(ids);
        using var scope = provider.CreateScope();
        scope.ServiceProvider.GetRequiredService<IScheduleService>().ShouldBeOfType<FakeScheduleService>();
        services.Count(d => d.ServiceType == typeof(IScheduleService)).ShouldBe(1);
    }

    [Fact]
    public void Calling_AddAsyncMonolith_twice_fails_with_guidance()
    {
        var services = Services();
        AddAsyncMonolith(services);

        var exception = Should.Throw<InvalidOperationException>(() => AddAsyncMonolith(services));

        exception.Message.ShouldContain("AddAsyncMonolithConsumers");
        services.Count(d => d.ServiceType == typeof(IHostedService)).ShouldBe(2);
    }
}
