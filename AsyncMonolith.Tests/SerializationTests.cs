using System.Text.Json;
using System.Text.Json.Serialization;
using AsyncMonolith.Tests.Infra;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;

namespace AsyncMonolith.Tests;

/// <summary>
///     Payload serialization, with the default and with custom JsonSerializerOptions.
/// </summary>
public class SerializationTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Payloads_use_System_Text_Json_defaults_unless_configured()
    {
        await using var host = await SqliteTestHost.Create();

        await host.Produce(new SerializationMessage { FirstName = "Ada", Priority = Priority.Urgent });

        (await host.ConsumerMessages()).ShouldHaveSingleItem().Payload
            .ShouldBe("""{"FirstName":"Ada","Priority":1}""");
    }

    [Fact]
    public async Task Custom_serializer_options_are_used_to_store_and_to_consume()
    {
        await using var host = await SqliteTestHost.Create(s => s.SerializerOptions =
            new JsonSerializerOptions(JsonSerializerDefaults.Web) { Converters = { new JsonStringEnumConverter() } });

        await host.Produce(new SerializationMessage { FirstName = "Ada", Priority = Priority.Urgent });
        (await host.ConsumerMessages()).ShouldHaveSingleItem().Payload
            .ShouldBe("""{"firstName":"Ada","priority":"Urgent"}""");

        (await host.ConsumerProcessor.ProcessBatch(Ct)).ShouldBe(1);

        var received = host.Services.GetRequiredService<SerializationRecorder>().Received.ShouldHaveSingleItem();
        received.FirstName.ShouldBe("Ada");
        received.Priority.ShouldBe(Priority.Urgent);
    }

    [Fact]
    public async Task Custom_serializer_options_are_used_for_schedules()
    {
        await using var host = await SqliteTestHost.Create(s => s.SerializerOptions =
            new JsonSerializerOptions(JsonSerializerDefaults.Web));

        await host.Schedule(new SerializationMessage { FirstName = "Ada" }, "0 * * * * *");

        (await host.ScheduledMessages()).ShouldHaveSingleItem().Payload.ShouldContain("\"firstName\"");
    }
}
