using System.Text.Json;
using AsyncMonolith.Producers;
using AsyncMonolith.Scheduling;
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
        var (fakeTime, _) = services.AddTestServices(AsyncMonolithSettings.Default);
        FakeTime = fakeTime;
        services.AddInMemoryDb();
        return services.BuildServiceProvider();
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
        var producer = serviceProvider.GetRequiredService<ProducerService<TestDbContext>>();
        var dbContext = serviceProvider.GetRequiredService<TestDbContext>();

        // When
        producer.Produce(consumerMessage, delay);
        await dbContext.SaveChangesAsync();

        // Then
        using var scope = serviceProvider.CreateScope();
        {
            var postDbContext = serviceProvider.GetRequiredService<TestDbContext>();
            var messages = await postDbContext.ConsumerMessages.ToListAsync();
            var message = Assert.Single(messages);
            message.AvailableAfter.Should().Be(delay);
            message.Attempts.Should().Be(0);
            message.Id.Should().Be("fake-id-0");
            message.ConsumerType = nameof(SingleConsumer);
            message.PayloadType = nameof(SingleConsumerMessage);
            message.Payload.Should().Be(JsonSerializer.Serialize(consumerMessage));
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
        var producer = serviceProvider.GetRequiredService<ProducerService<TestDbContext>>();
        var dbContext = serviceProvider.GetRequiredService<TestDbContext>();

        // When
        producer.Produce(consumerMessage, delay);
        await dbContext.SaveChangesAsync();

        // Then
        using var scope = serviceProvider.CreateScope();
        {
            var postDbContext = serviceProvider.GetRequiredService<TestDbContext>();
            var messages = await postDbContext.ConsumerMessages.ToListAsync();
            messages.Count.Should().Be(2);

            var message1 = Assert.Single(messages.Where(m => m.ConsumerType == nameof(MultiConsumer1)));
            message1.AvailableAfter.Should().Be(delay);
            message1.Attempts.Should().Be(0);
            message1.Id.Should().Be("fake-id-0");
            message1.ConsumerType = nameof(MultiConsumer1);
            message1.PayloadType = nameof(MultiConsumerMessage);
            message1.Payload.Should().Be(JsonSerializer.Serialize(consumerMessage));

            var message2 = Assert.Single(messages.Where(m => m.ConsumerType == nameof(MultiConsumer2)));
            message2.AvailableAfter.Should().Be(delay);
            message2.Attempts.Should().Be(0);
            message2.Id.Should().Be("fake-id-1");
            message2.ConsumerType = nameof(MultiConsumer2);
            message2.PayloadType = nameof(MultiConsumerMessage);
            message2.Payload.Should().Be(JsonSerializer.Serialize(consumerMessage));
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

        var producer = serviceProvider.GetRequiredService<ProducerService<TestDbContext>>();
        var dbContext = serviceProvider.GetRequiredService<TestDbContext>();

        // When
        producer.Produce(consumerMessage);
        await dbContext.SaveChangesAsync();

        // Then
        using var scope = serviceProvider.CreateScope();
        {
            var postDbContext = serviceProvider.GetRequiredService<TestDbContext>();
            var messages = await postDbContext.ConsumerMessages.ToListAsync();
            var message = Assert.Single(messages);
            message.AvailableAfter.Should().Be(FakeTime.GetUtcNow().ToUnixTimeSeconds());
            message.Attempts.Should().Be(0);
            message.Id.Should().Be("fake-id-0");
            message.ConsumerType = nameof(SingleConsumer);
            message.PayloadType = nameof(SingleConsumerMessage);
            message.Payload.Should().Be(JsonSerializer.Serialize(consumerMessage));
        }
    }

    [Fact]
    public async Task Producer_Produces_Single_Scheduled_Message()
    {
        // Given
        var serviceProvider = Setup();
        var message = new SingleConsumerMessage
        {
            Name = "test-name"
        };
        var expectedPayload = JsonSerializer.Serialize(message);

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

        var producer = serviceProvider.GetRequiredService<ProducerService<TestDbContext>>();
        var dbContext = serviceProvider.GetRequiredService<TestDbContext>();

        // When
        producer.Produce(scheduledMessage);
        await dbContext.SaveChangesAsync();

        // Then
        using var scope = serviceProvider.CreateScope();
        {
            var postDbContext = serviceProvider.GetRequiredService<TestDbContext>();
            var messages = await postDbContext.ConsumerMessages.ToListAsync();
            var producedMessage = Assert.Single(messages);
            producedMessage.AvailableAfter.Should().Be(FakeTime.GetUtcNow().ToUnixTimeSeconds());
            producedMessage.Attempts.Should().Be(0);
            producedMessage.Id.Should().Be("fake-id-0");
            producedMessage.ConsumerType = nameof(SingleConsumer);
            producedMessage.PayloadType = nameof(SingleConsumerMessage);
            producedMessage.Payload.Should().Be(expectedPayload);
        }
    }

    [Fact]
    public async Task Producer_Produces_Multiple_Scheduled_Message()
    {
        // Given
        var serviceProvider = Setup();
        var message = new MultiConsumerMessage
        {
            Name = "test-name"
        };
        var payload = JsonSerializer.Serialize(message);

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

        var producer = serviceProvider.GetRequiredService<ProducerService<TestDbContext>>();
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

            var message1 = Assert.Single(messages.Where(m => m.ConsumerType == nameof(MultiConsumer1)));
            message1.AvailableAfter.Should().Be(FakeTime.GetUtcNow().ToUnixTimeSeconds());
            message1.Attempts.Should().Be(0);
            message1.Id.Should().Be("fake-id-0");
            message1.ConsumerType = nameof(MultiConsumer1);
            message1.PayloadType = nameof(MultiConsumerMessage);
            message1.Payload.Should().Be(JsonSerializer.Serialize(message));

            var message2 = Assert.Single(messages.Where(m => m.ConsumerType == nameof(MultiConsumer2)));
            message2.AvailableAfter.Should().Be(FakeTime.GetUtcNow().ToUnixTimeSeconds());
            message2.Attempts.Should().Be(0);
            message2.Id.Should().Be("fake-id-1");
            message2.ConsumerType = nameof(MultiConsumer2);
            message2.PayloadType = nameof(MultiConsumerMessage);
            message2.Payload.Should().Be(JsonSerializer.Serialize(message));
        }
    }
}