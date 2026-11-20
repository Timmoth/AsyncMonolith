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
    public async Task Producing_an_insert_id_that_is_already_pending_is_skipped()
    {
        await using var host = await SqliteTestHost.Create();
        await host.Produce(new SingleConsumerMessage { Name = "first" }, insertId: "dedupe-key");

        await host.Produce(new SingleConsumerMessage { Name = "second" }, insertId: "dedupe-key");

        JsonSerializer.Deserialize<SingleConsumerMessage>((await host.ConsumerMessages()).ShouldHaveSingleItem().Payload)!
            .Name.ShouldBe("first");
    }

    [Fact]
    public async Task Producing_an_insert_id_twice_before_saving_adds_it_once()
    {
        await using var host = await SqliteTestHost.Create();

        using (var scope = host.Services.CreateScope())
        {
            var producer = scope.ServiceProvider.GetRequiredService<IProducerService>();
            await producer.Produce(new MultiConsumerMessage { Name = "first" }, insertId: "dedupe-key", cancellationToken: Ct);
            await producer.Produce(new MultiConsumerMessage { Name = "second" }, insertId: "dedupe-key", cancellationToken: Ct);
            await scope.ServiceProvider.GetRequiredService<TestDbContext>().SaveChangesAsync(Ct);
        }

        (await host.ConsumerMessages()).Count.ShouldBe(2);
        (await host.ConsumerMessages()).ShouldAllBe(m => m.Payload.Contains("first", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Only_consumers_that_already_have_the_insert_id_pending_are_skipped()
    {
        await using var host = await SqliteTestHost.Create();
        await host.Produce(new MultiConsumerMessage { Name = "first" }, insertId: "dedupe-key");
        // MultiConsumer1 has processed its message; MultiConsumer2's is still pending.
        await host.Change(db => db.ConsumerMessages.Remove(
            db.ConsumerMessages.Single(m => m.ConsumerType == nameof(MultiConsumer1))));

        await host.Produce(new MultiConsumerMessage { Name = "second" }, insertId: "dedupe-key");

        var messages = await host.ConsumerMessages();
        messages.Single(m => m.ConsumerType == nameof(MultiConsumer1)).Payload.ShouldContain("second");
        messages.Single(m => m.ConsumerType == nameof(MultiConsumer2)).Payload.ShouldContain("first");
    }

    [Fact]
    public async Task An_insert_id_can_be_produced_again_once_its_messages_are_consumed()
    {
        await using var host = await SqliteTestHost.Create();
        await host.Produce(new SingleConsumerMessage { Name = "first" }, insertId: "dedupe-key");
        await host.ConsumerProcessor.ProcessBatch(Ct);

        await host.Produce(new SingleConsumerMessage { Name = "again" }, insertId: "dedupe-key");

        (await host.ConsumerMessages()).ShouldHaveSingleItem().Payload.ShouldContain("again");
    }

    [Fact]
    public async Task Two_producers_racing_on_one_insert_id_are_stopped_by_the_unique_index()
    {
        await using var host = await SqliteTestHost.Create();
        using var first = host.Services.CreateScope();
        using var second = host.Services.CreateScope();

        // Both check before either has saved
        await first.ServiceProvider.GetRequiredService<IProducerService>()
            .Produce(new SingleConsumerMessage { Name = "first" }, insertId: "dedupe-key", cancellationToken: Ct);
        await second.ServiceProvider.GetRequiredService<IProducerService>()
            .Produce(new SingleConsumerMessage { Name = "second" }, insertId: "dedupe-key", cancellationToken: Ct);

        await first.ServiceProvider.GetRequiredService<TestDbContext>().SaveChangesAsync(Ct);
        await Should.ThrowAsync<DbUpdateException>(() =>
            second.ServiceProvider.GetRequiredService<TestDbContext>().SaveChangesAsync(Ct));
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
