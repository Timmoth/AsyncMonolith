using AsyncMonolith.Consumers;
using AsyncMonolith.Producers;
using AsyncMonolith.Scheduling;
using AsyncMonolith.TestHelpers;
using AsyncMonolith.Tests.Infra;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;

namespace AsyncMonolith.Tests;

/// <summary>
///     The shipped AsyncMonolith.TestHelpers package.
/// </summary>
public class TestHelpersTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static ServiceProvider FakeServices()
    {
        var services = new ServiceCollection();
        services.AddSingleton(TimeProvider.System);
        services.AddFakeAsyncMonolithBaseServices(s => s.RegisterTypesFromAssemblyContaining<TestHelpersTests>());
        return services.BuildServiceProvider();
    }

    [Fact]
    public async Task FakeProducerService_records_a_message_per_consumer()
    {
        using var scope = FakeServices().CreateScope();
        var producer = (FakeProducerService)scope.ServiceProvider.GetRequiredService<IProducerService>();

        await producer.Produce(new MultiConsumerMessage { Name = "x" }, deduplicationKey: "insert", cancellationToken: Ct);
        await producer.ProduceList([new RailConsumerMessage()], cancellationToken: Ct);

        producer.CreatedConsumerMessages.Select(m => m.ConsumerType).Order().ShouldBe([
            nameof(MultiConsumer1), nameof(MultiConsumer2),
            nameof(RailConsumer0), nameof(RailConsumer1), nameof(RailConsumer2)
        ]);
        producer.CreatedConsumerMessages.Where(m => m.PayloadType == nameof(MultiConsumerMessage))
            .ShouldAllBe(m => m.DeduplicationKey == "insert");
        producer.CreatedConsumerMessages.Single(m => m.ConsumerType == nameof(RailConsumer2)).RailId.ShouldBe(2);
    }

    [Fact]
    public async Task FakeScheduleService_records_schedules_and_deletions()
    {
        using var scope = FakeServices().CreateScope();
        var schedules = (FakeScheduleService)scope.ServiceProvider.GetRequiredService<IScheduleService>();

        var id = schedules.Schedule(new SingleConsumerMessage { Name = "x" }, "0 0 9 * * *", "UTC", "tag");
        await schedules.DeleteByTag("tag", Ct);
        await schedules.DeleteById(id, Ct);

        var scheduled = schedules.CreatedScheduledMessages.ShouldHaveSingleItem();
        scheduled.Id.ShouldBe(id);
        scheduled.Tag.ShouldBe("tag");
        schedules.DeletedScheduledMessageTags.ShouldBe(["tag"]);
        schedules.DeletedScheduledMessageIds.ShouldBe([id]);
    }

    [Fact]
    public async Task AssertSingleConsumerMessage_finds_the_message_for_a_consumer()
    {
        await using var host = await SqliteTestHost.Create();
        await host.Produce(new MultiConsumerMessage { Name = "x" });

        using var scope = host.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<TestDbContext>();

        var message = await db.AssertSingleConsumerMessage<MultiConsumer2, MultiConsumerMessage>(
            new MultiConsumerMessage { Name = "x" });

        message.ConsumerType.ShouldBe(nameof(MultiConsumer2));
        (await db.GetConsumerMessages<MultiConsumerMessage>()).Count.ShouldBe(2);
    }

    [Fact]
    public async Task ProcessNext_consumes_and_removes_the_next_message()
    {
        await using var host = await SqliteTestHost.Create();
        await host.Produce(new SingleConsumerMessage { Name = "x" });

        using (var scope = host.Services.CreateScope())
        {
            var processed = await TestConsumerMessageProcessor.ProcessNext<TestDbContext>(scope, Ct);
            processed.ShouldNotBeNull().ConsumerType.ShouldBe(nameof(SingleConsumer));
        }

        host.Invocations.GetInvocationCount(nameof(SingleConsumer)).ShouldBe(1);
        (await host.ConsumerMessages()).ShouldBeEmpty();
    }

    [Fact]
    public async Task ProcessNext_for_a_consumer_processes_only_that_consumers_message()
    {
        await using var host = await SqliteTestHost.Create();
        await host.Produce(new MultiConsumerMessage { Name = "x" });

        using (var scope = host.Services.CreateScope())
        {
            var processed = await TestConsumerMessageProcessor.ProcessNext<TestDbContext, MultiConsumer2>(scope, Ct);
            processed.ShouldNotBeNull().ConsumerType.ShouldBe(nameof(MultiConsumer2));
        }

        host.Invocations.GetInvocationCount(nameof(MultiConsumer1)).ShouldBe(0);
        host.Invocations.GetInvocationCount(nameof(MultiConsumer2)).ShouldBe(1);
        (await host.ConsumerMessages()).ShouldHaveSingleItem().ConsumerType.ShouldBe(nameof(MultiConsumer1));
    }

    [Fact]
    public async Task Process_invokes_a_consumer_directly_with_a_payload()
    {
        await using var host = await SqliteTestHost.Create();
        using var scope = host.Services.CreateScope();

        await TestConsumerMessageProcessor.Process<OrderedConsumer, OrderedMessage>(
            scope, new OrderedMessage { Name = "direct" }, Ct);

        host.Invocations.Order.ShouldBe(["OrderedConsumer:direct"]);
        (await host.ConsumerMessages()).ShouldBeEmpty();
    }
}
