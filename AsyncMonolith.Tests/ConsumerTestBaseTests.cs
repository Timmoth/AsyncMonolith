using AsyncMonolith.TestHelpers;
using AsyncMonolith.Tests.Infra;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;

namespace AsyncMonolith.Tests;

/// <summary>
///     The TestHelpers ConsumerTestBase, used as a consumer test would use it.
/// </summary>
public class ConsumerTestBaseTests(ITestOutputHelper output) : ConsumerTestBase(output)
{
    protected override Task Setup(IServiceCollection services)
    {
        services.AddSingleton<TestConsumerInvocations>();
        services.AddScoped<OrderedConsumer>();
        return Task.CompletedTask;
    }

    [Fact]
    public async Task Process_runs_the_consumer_with_the_payload()
    {
        await Process<OrderedConsumer, OrderedMessage>(new OrderedMessage { Name = "from base" },
            TestContext.Current.CancellationToken);

        Services.GetRequiredService<TestConsumerInvocations>().Order.ShouldBe(["OrderedConsumer:from base"]);
    }

    [Fact]
    public void A_fake_clock_is_registered()
    {
        Services.GetRequiredService<TimeProvider>().ShouldBeSameAs(FakeTime);
    }
}
