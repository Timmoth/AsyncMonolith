using System.Diagnostics;
using System.Text.Json;
using AsyncMonolith.Producers;
using AsyncMonolith.TestHelpers;
using AsyncMonolith.Tests.Infra;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;

namespace AsyncMonolith.Tests;

/// <summary>
///     The producer against each real database: the same outbox and deduplication behaviour everywhere.
/// </summary>
[Trait("Category", "Integration")]
public class ProducerServiceDbTests : DbTestsBase
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private async Task Run(DbType dbType, Func<ServiceProvider, Task> test)
    {
        var dbContainer = GetTestDbContainer(dbType);
        try
        {
            await test(await Setup(dbContainer));
        }
        finally
        {
            await dbContainer.DisposeAsync();
        }
    }

    private static async Task Produce(ServiceProvider services, Func<IProducerService, Task> produce)
    {
        using var scope = services.CreateScope();
        await produce(scope.ServiceProvider.GetRequiredService<IProducerService>());
        await scope.ServiceProvider.GetRequiredService<TestDbContext>().SaveChangesAsync(Ct);
    }

    private static async Task<List<Consumers.ConsumerMessage>> Messages(ServiceProvider services)
    {
        using var scope = services.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<TestDbContext>().ConsumerMessages
            .OrderBy(m => m.Id).ToListAsync(Ct);
    }

    [Theory]
    [MemberData(nameof(GetTestDbTypes))]
    public Task Produce_writes_the_message_when_the_caller_saves(DbType dbType) => Run(dbType, async services =>
    {
        var payload = new SingleConsumerMessage { Name = "test-name" };
        using var activity = GetActivity();
        Activity.Current = activity;

        await Produce(services, p => p.Produce(payload, TimeSpan.FromSeconds(10), cancellationToken: Ct));

        using var scope = services.CreateScope();
        var message = await scope.ServiceProvider.GetRequiredService<TestDbContext>()
            .AssertSingleConsumerMessage<SingleConsumer, SingleConsumerMessage>(payload);
        message.AvailableAfter.ShouldBe(FakeTime.GetUtcNow().ToUnixTimeSeconds() + 10);
        message.Attempts.ShouldBe(0);
        message.Payload.ShouldBe(JsonSerializer.Serialize(payload));
        message.TraceId.ShouldBe(activity?.TraceId.ToString());
        message.SpanId.ShouldBe(activity?.SpanId.ToString());
    });

    [Theory]
    [MemberData(nameof(GetTestDbTypes))]
    public Task Produce_writes_nothing_until_the_caller_saves(DbType dbType) => Run(dbType, async services =>
    {
        using (var scope = services.CreateScope())
        {
            await scope.ServiceProvider.GetRequiredService<IProducerService>()
                .Produce(new SingleConsumerMessage { Name = "unsaved" }, cancellationToken: Ct);
        }

        (await Messages(services)).ShouldBeEmpty();
    });

    [Theory]
    [MemberData(nameof(GetTestDbTypes))]
    public Task ProduceList_writes_one_message_per_payload(DbType dbType) => Run(dbType, async services =>
    {
        await Produce(services, p => p.ProduceList(
            [new SingleConsumerMessage { Name = "a" }, new SingleConsumerMessage { Name = "b" }], TimeSpan.FromSeconds(10),
            cancellationToken: Ct));

        var messages = await Messages(services);
        messages.Count.ShouldBe(2);
        messages.Select(m => m.DeduplicationKey).Distinct().Count().ShouldBe(2);
        messages.ShouldAllBe(m => m.AvailableAfter == FakeTime.GetUtcNow().ToUnixTimeSeconds() + 10);
    });

    [Theory]
    [MemberData(nameof(GetTestDbTypes))]
    public Task Produce_skips_an_insert_id_that_is_already_pending(DbType dbType) => Run(dbType, async services =>
    {
        await Produce(services, p => p.Produce(new SingleConsumerMessage { Name = "first" }, deduplicationKey: "key",
            cancellationToken: Ct));
        await Produce(services, p => p.Produce(new SingleConsumerMessage { Name = "second" }, deduplicationKey: "key",
            cancellationToken: Ct));

        (await Messages(services)).ShouldHaveSingleItem().Payload.ShouldContain("first");
    });

    [Theory]
    [MemberData(nameof(GetTestDbTypes))]
    public Task Racing_producers_are_stopped_by_the_unique_index(DbType dbType) => Run(dbType, async services =>
    {
        using var first = services.CreateScope();
        using var second = services.CreateScope();
        await first.ServiceProvider.GetRequiredService<IProducerService>()
            .Produce(new SingleConsumerMessage { Name = "first" }, deduplicationKey: "key", cancellationToken: Ct);
        await second.ServiceProvider.GetRequiredService<IProducerService>()
            .Produce(new SingleConsumerMessage { Name = "second" }, deduplicationKey: "key", cancellationToken: Ct);

        await first.ServiceProvider.GetRequiredService<TestDbContext>().SaveChangesAsync(Ct);
        await Should.ThrowAsync<DbUpdateException>(() =>
            second.ServiceProvider.GetRequiredService<TestDbContext>().SaveChangesAsync(Ct));
    });
}
