using System.Diagnostics;
using System.Text.Json;
using AsyncMonolith.Producers;
using AsyncMonolith.Scheduling;
using AsyncMonolith.TestHelpers;
using AsyncMonolith.Tests.Infra;
using AsyncMonolith.Utilities;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Time.Testing;

namespace AsyncMonolith.Tests;

public class ProducerServiceTests
{
    public FakeTimeProvider FakeTime = default!;

    private ServiceProvider Setup()
    {
        var services = new ServiceCollection();
        var (fakeTime, _) = services.AddTestServices(DbType.Ef, AsyncMonolithSettings.Default);
        FakeTime = fakeTime;
        services.AddInMemoryDb();
        return services.BuildServiceProvider();
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

    [Fact]
    public async Task Producer_Writes_Single_Consumer_Message()
    {
        // Given
        var serviceProvider = Setup();
        var consumerMessage = new SingleConsumerMessage
        {
            Name = "test-name"
        };

        var delay = 100;
        var insertId = "test-insert_id";
        var producer = serviceProvider.GetRequiredService<IProducerService>();
        var dbContext = serviceProvider.GetRequiredService<TestDbContext>();
        using var activity = GetActivity();
        Activity.Current = activity;

        // When
        await producer.Produce(consumerMessage, delay, insertId);
        await dbContext.SaveChangesAsync();

        // Then
        using var scope = serviceProvider.CreateScope();
        {
            var postDbContext = serviceProvider.GetRequiredService<TestDbContext>();
            var message =
                await postDbContext.AssertSingleConsumerMessage<SingleConsumer, SingleConsumerMessage>(consumerMessage);
            message.AvailableAfter.Should().Be(delay);
            message.Attempts.Should().Be(0);
            message.Id.Should().Be("fake-id-0");
            message.ConsumerType = nameof(SingleConsumer);
            message.PayloadType = nameof(SingleConsumerMessage);
            message.Payload.Should().Be(JsonSerializer.Serialize(consumerMessage));
            message.InsertId.Should().Be(insertId);
            message.TraceId.Should().Be(activity?.TraceId.ToString());
            message.SpanId.Should().Be(activity?.SpanId.ToString());
        }
    }

    [Fact]
    public async Task Producer_Writes_Multiple_Consumer_Message()
    {
        // Given
        var serviceProvider = Setup();
        var consumerMessage = new MultiConsumerMessage
        {
            Name = "test-name"
        };

        var delay = 100;
        var insertId = "test-insert_id";
        var producer = serviceProvider.GetRequiredService<IProducerService>();
        var dbContext = serviceProvider.GetRequiredService<TestDbContext>();
        using var activity = GetActivity();
        Activity.Current = activity;

        // When
        await producer.Produce(consumerMessage, delay, insertId);
        await dbContext.SaveChangesAsync();

        // Then
        using var scope = serviceProvider.CreateScope();
        {
            var postDbContext = serviceProvider.GetRequiredService<TestDbContext>();
            var message1 =
                await postDbContext.AssertSingleConsumerMessage<MultiConsumer1, MultiConsumerMessage>(consumerMessage);
            message1.AvailableAfter.Should().Be(delay);
            message1.Attempts.Should().Be(0);
            message1.Id.Should().Be("fake-id-0");
            message1.ConsumerType = nameof(MultiConsumer1);
            message1.PayloadType = nameof(MultiConsumerMessage);
            message1.Payload.Should().Be(JsonSerializer.Serialize(consumerMessage));
            message1.InsertId.Should().Be(insertId);
            message1.TraceId.Should().Be(activity?.TraceId.ToString());
            message1.SpanId.Should().Be(activity?.SpanId.ToString());

            var message2 =
                await postDbContext.AssertSingleConsumerMessage<MultiConsumer2, MultiConsumerMessage>(consumerMessage);
            message2.AvailableAfter.Should().Be(delay);
            message2.Attempts.Should().Be(0);
            message2.Id.Should().Be("fake-id-1");
            message2.ConsumerType = nameof(MultiConsumer2);
            message2.PayloadType = nameof(MultiConsumerMessage);
            message2.Payload.Should().Be(JsonSerializer.Serialize(consumerMessage));
            message2.InsertId.Should().Be(insertId);
            message2.TraceId.Should().Be(activity?.TraceId.ToString());
            message2.SpanId.Should().Be(activity?.SpanId.ToString());
        }
    }

    [Fact]
    public async Task Producer_AvailableAfter_Defaults_To_CurrentTime()
    {
        // Given
        var serviceProvider = Setup();
        var consumerMessage = new SingleConsumerMessage
        {
            Name = "test-name"
        };

        var producer = serviceProvider.GetRequiredService<IProducerService>();
        var dbContext = serviceProvider.GetRequiredService<TestDbContext>();
        using var activity = GetActivity();
        Activity.Current = activity;

        // When
        await producer.Produce(consumerMessage);
        await dbContext.SaveChangesAsync();

        // Then
        using var scope = serviceProvider.CreateScope();
        {
            var postDbContext = serviceProvider.GetRequiredService<TestDbContext>();
            var message =
                await postDbContext.AssertSingleConsumerMessage<SingleConsumer, SingleConsumerMessage>(consumerMessage);
            message.AvailableAfter.Should().Be(FakeTime.GetUtcNow().ToUnixTimeSeconds());
            message.Attempts.Should().Be(0);
            message.Id.Should().Be("fake-id-1");
            message.ConsumerType = nameof(SingleConsumer);
            message.PayloadType = nameof(SingleConsumerMessage);
            message.Payload.Should().Be(JsonSerializer.Serialize(consumerMessage));
            message.InsertId.Should().Be("fake-id-0");
            message.TraceId.Should().Be(activity?.TraceId.ToString());
            message.SpanId.Should().Be(activity?.SpanId.ToString());
        }
    }

    [Fact]
    public async Task Producer_Produces_Single_Scheduled_Message()
    {
        // Given
        var serviceProvider = Setup();
        var consumerMessage = new SingleConsumerMessage
        {
            Name = "test-name"
        };
        var expectedPayload = JsonSerializer.Serialize(consumerMessage);

        var scheduledMessage = new ScheduledMessage
        {
            Id = "test-id",
            Tag = "test-tag",
            AvailableAfter = FakeTime.GetUtcNow().ToUnixTimeSeconds(),
            ChronExpression = "* * * * *",
            ChronTimezone = "",
            PayloadType = nameof(SingleConsumerMessage),
            Payload = expectedPayload
        };

        var producer = serviceProvider.GetRequiredService<IProducerService>();
        var dbContext = serviceProvider.GetRequiredService<TestDbContext>();
        using var activity = GetActivity();
        Activity.Current = activity;

        // When
        producer.Produce(scheduledMessage);
        await dbContext.SaveChangesAsync();

        // Then
        using var scope = serviceProvider.CreateScope();
        {
            var postDbContext = serviceProvider.GetRequiredService<TestDbContext>();
            var message =
                await postDbContext.AssertSingleConsumerMessage<SingleConsumer, SingleConsumerMessage>(consumerMessage);
            message.AvailableAfter.Should().Be(FakeTime.GetUtcNow().ToUnixTimeSeconds());
            message.Attempts.Should().Be(0);
            message.InsertId.Should().Be("fake-id-0");
            message.Id.Should().Be("fake-id-1");
            message.ConsumerType = nameof(SingleConsumer);
            message.PayloadType = nameof(SingleConsumerMessage);
            message.Payload.Should().Be(expectedPayload);
            message.TraceId.Should().BeNull();
            message.SpanId.Should().BeNull();
        }
    }

    [Fact]
    public async Task Producer_Produces_Multiple_Scheduled_Message()
    {
        // Given
        var serviceProvider = Setup();
        var consumerMessage = new MultiConsumerMessage
        {
            Name = "test-name"
        };
        var payload = JsonSerializer.Serialize(consumerMessage);

        var scheduledMessage = new ScheduledMessage
        {
            Id = "test-id",
            Tag = "test-tag",
            AvailableAfter = FakeTime.GetUtcNow().ToUnixTimeSeconds(),
            ChronExpression = "* * * * *",
            ChronTimezone = "UTC",
            PayloadType = nameof(MultiConsumerMessage),
            Payload = payload
        };

        var producer = serviceProvider.GetRequiredService<IProducerService>();
        var dbContext = serviceProvider.GetRequiredService<TestDbContext>();

        // When
        producer.Produce(scheduledMessage);
        await dbContext.SaveChangesAsync();

        // Then
        using var scope = serviceProvider.CreateScope();
        {
            var postDbContext = serviceProvider.GetRequiredService<TestDbContext>();
            var messages = await postDbContext.ConsumerMessages.ToListAsync();
            messages.Count.Should().Be(2);

            var message1 =
                await postDbContext.AssertSingleConsumerMessage<MultiConsumer1, MultiConsumerMessage>(consumerMessage);
            message1.AvailableAfter.Should().Be(FakeTime.GetUtcNow().ToUnixTimeSeconds());
            message1.Attempts.Should().Be(0);
            message1.InsertId.Should().Be("fake-id-0");
            message1.Id.Should().Be("fake-id-1");
            message1.ConsumerType = nameof(MultiConsumer1);
            message1.PayloadType = nameof(MultiConsumerMessage);
            message1.Payload.Should().Be(JsonSerializer.Serialize(consumerMessage));
            message1.TraceId.Should().BeNull();
            message1.SpanId.Should().BeNull();
            var message2 =
                await postDbContext.AssertSingleConsumerMessage<MultiConsumer2, MultiConsumerMessage>(consumerMessage);
            message2.AvailableAfter.Should().Be(FakeTime.GetUtcNow().ToUnixTimeSeconds());
            message2.Attempts.Should().Be(0);
            message2.InsertId.Should().Be("fake-id-0");
            message2.Id.Should().Be("fake-id-2");
            message2.ConsumerType = nameof(MultiConsumer2);
            message2.PayloadType = nameof(MultiConsumerMessage);
            message2.Payload.Should().Be(JsonSerializer.Serialize(consumerMessage));
            message2.TraceId.Should().BeNull();
            message2.SpanId.Should().BeNull();
        }
    }
}