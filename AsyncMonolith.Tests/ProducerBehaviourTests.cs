using System.Text.Json;
using AsyncMonolith.Producers;
using AsyncMonolith.Tests.Infra;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;

namespace AsyncMonolith.Tests;

/// <summary>
///     The EF producer against in-memory SQLite: what it writes, and its transactional outbox guarantees.
/// </summary>
public class ProducerBehaviourTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Produce_writes_one_pending_message_per_consumer()
    {
        await using var host = await SqliteTestHost.Create();

        await host.Produce(new MultiConsumerMessage { Name = "hello" });

        var messages = await host.ConsumerMessages();
        messages.Select(m => m.ConsumerType).Order().ShouldBe([nameof(MultiConsumer1), nameof(MultiConsumer2)]);
        messages.ShouldAllBe(m => m.PayloadType == nameof(MultiConsumerMessage));
        messages.ShouldAllBe(m => m.Attempts == 0);
        messages.ShouldAllBe(m => m.CreatedAt == host.Now && m.AvailableAfter == host.Now);
        messages.Select(m => m.InsertId).Distinct().ShouldHaveSingleItem();
        messages.Select(m => m.Id).Distinct().Count().ShouldBe(2);
        JsonSerializer.Deserialize<MultiConsumerMessage>(messages[0].Payload)!.Name.ShouldBe("hello");
    }

    [Fact]
    public async Task Produce_uses_the_given_availability_and_insert_id()
    {
        await using var host = await SqliteTestHost.Create();

        await host.Produce(new SingleConsumerMessage { Name = "x" }, host.Now + 300, "my-insert-id");

        var message = (await host.ConsumerMessages()).ShouldHaveSingleItem();
        message.AvailableAfter.ShouldBe(host.Now + 300);
        message.InsertId.ShouldBe("my-insert-id");
    }

    [Fact]
    public async Task Produce_assigns_each_consumer_its_rail()
    {
        await using var host = await SqliteTestHost.Create();

        await host.Produce(new RailConsumerMessage());

        (await host.ConsumerMessages()).Select(m => (m.ConsumerType, m.RailId)).OrderBy(x => x.RailId).ShouldBe([
            (nameof(RailConsumer0), 0),
            (nameof(RailConsumer1), 1),
            (nameof(RailConsumer2), 2)
        ]);
    }

    [Fact]
    public async Task Producing_the_same_insert_id_twice_is_rejected_by_the_database()
    {
        await using var host = await SqliteTestHost.Create();
        await host.Produce(new SingleConsumerMessage { Name = "first" }, insertId: "dedupe-key");

        await Should.ThrowAsync<DbUpdateException>(() =>
            host.Produce(new SingleConsumerMessage { Name = "second" }, insertId: "dedupe-key"));

        JsonSerializer.Deserialize<SingleConsumerMessage>((await host.ConsumerMessages()).ShouldHaveSingleItem().Payload)!
            .Name.ShouldBe("first");
    }

    [Fact]
    public async Task ProduceList_gives_each_message_its_own_insert_id()
    {
        await using var host = await SqliteTestHost.Create();

        using (var scope = host.Services.CreateScope())
        {
            await scope.ServiceProvider.GetRequiredService<IProducerService>().ProduceList(
                [new MultiConsumerMessage { Name = "a" }, new MultiConsumerMessage { Name = "b" }],
                cancellationToken: Ct);
            await scope.ServiceProvider.GetRequiredService<TestDbContext>().SaveChangesAsync(Ct);
        }

        var messages = await host.ConsumerMessages();
        messages.Count.ShouldBe(4);
        messages.GroupBy(m => m.InsertId).ShouldAllBe(g => g.Count() == 2);
        messages.Select(m => m.InsertId).Distinct().Count().ShouldBe(2);
    }

    [Fact]
    public async Task Produced_message_is_not_written_until_the_caller_saves()
    {
        await using var host = await SqliteTestHost.Create();

        using (var scope = host.Services.CreateScope())
        {
            await scope.ServiceProvider.GetRequiredService<IProducerService>()
                .Produce(new SingleConsumerMessage { Name = "x" }, cancellationToken: Ct);
        }

        (await host.ConsumerMessages()).ShouldBeEmpty();
    }

    [Fact]
    public async Task Produced_message_is_discarded_when_the_callers_transaction_rolls_back()
    {
        await using var host = await SqliteTestHost.Create();

        using (var scope = host.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<TestDbContext>();
            await using var transaction = await db.Database.BeginTransactionAsync(Ct);
            await scope.ServiceProvider.GetRequiredService<IProducerService>()
                .Produce(new SingleConsumerMessage { Name = "x" }, cancellationToken: Ct);
            await db.SaveChangesAsync(Ct);
            await transaction.RollbackAsync(Ct);
        }

        (await host.ConsumerMessages()).ShouldBeEmpty();
    }

    [Fact]
    public async Task Produced_message_is_kept_when_the_callers_transaction_commits()
    {
        await using var host = await SqliteTestHost.Create();

        using (var scope = host.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<TestDbContext>();
            await using var transaction = await db.Database.BeginTransactionAsync(Ct);
            await scope.ServiceProvider.GetRequiredService<IProducerService>()
                .Produce(new SingleConsumerMessage { Name = "x" }, cancellationToken: Ct);
            await db.SaveChangesAsync(Ct);
            await transaction.CommitAsync(Ct);
        }

        (await host.ConsumerMessages()).ShouldHaveSingleItem();
    }
}
