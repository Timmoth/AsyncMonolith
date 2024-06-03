using System.Text.Json;
using AsnyMonolith.Scheduling;
using AsyncMonolith.Tests.Infra;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Time.Testing;
using Testcontainers.PostgreSql;

namespace AsyncMonolith.Tests;

public class ScheduledMessageProcessorTests : IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgreSqlContainer = new PostgreSqlBuilder().Build();
    private TestConsumerInvocations _testConsumerInvocations = default!;
    public FakeTimeProvider FakeTime = default!;

    public Task InitializeAsync()
    {
        return _postgreSqlContainer.StartAsync();
    }

    public Task DisposeAsync()
    {
        return _postgreSqlContainer.DisposeAsync().AsTask();
    }

    private async Task<ServiceProvider> Setup()
    {
        var services = new ServiceCollection();
        services.AddTestServices();
        services.AddDbContext<TestDbContext>((sp, options) =>
            {
                options.UseNpgsql(_postgreSqlContainer.GetConnectionString(), o => { });
            }
        );
        var (fakeTime, invocations) = services.AddTestServices();
        _testConsumerInvocations = invocations;
        FakeTime = fakeTime;

        services.AddSingleton<ScheduledMessageProcessor<TestDbContext>>();
        var serviceProvider = services.BuildServiceProvider();

        using var scope = serviceProvider.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<TestDbContext>();
        await dbContext.Database.EnsureCreatedAsync();

        await dbContext.SaveChangesAsync();

        return serviceProvider;
    }


    [Fact]
    public async Task ScheduledMessageProcessor_Produces_ConsumerMessages()
    {
        // Given
        var serviceProvider = await Setup();

        var consumerMessage = new MultiConsumerMessage
        {
            Name = "test-name"
        };
        var serializedMessage = JsonSerializer.Serialize(consumerMessage);

        using (var scope = serviceProvider.CreateScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<TestDbContext>();
            var scheduledMessageService =
                scope.ServiceProvider.GetRequiredService<ScheduledMessageService<TestDbContext>>();

            scheduledMessageService.Schedule(consumerMessage, 0, "test-tag");

            await dbContext.SaveChangesAsync();
        }

        // When
        var processor = serviceProvider.GetRequiredService<ScheduledMessageProcessor<TestDbContext>>();

        var consumedMessage = await processor.ConsumeNext(CancellationToken.None);

        // Then
        consumedMessage.Should().BeTrue();
        using (var scope = serviceProvider.CreateScope())
        {
            var postDbContext = serviceProvider.GetRequiredService<TestDbContext>();
            var messages = await postDbContext.ConsumerMessages.ToListAsync();
            messages.Count.Should().Be(2);

            var message1 = Assert.Single(messages.Where(m => m.ConsumerType == nameof(MultiConsumer1)));
            message1.AvailableAfter.Should().Be(FakeTime.GetUtcNow().ToUnixTimeSeconds());
            message1.Attempts.Should().Be(0);
            message1.Id.Should().Be("fake-id-1");
            message1.ConsumerType = nameof(MultiConsumer1);
            message1.PayloadType = nameof(MultiConsumerMessage);
            message1.Payload.Should().Be(serializedMessage);

            var message2 = Assert.Single(messages.Where(m => m.ConsumerType == nameof(MultiConsumer2)));
            message2.AvailableAfter.Should().Be(FakeTime.GetUtcNow().ToUnixTimeSeconds());
            message2.Attempts.Should().Be(0);
            message2.Id.Should().Be("fake-id-2");
            message2.ConsumerType = nameof(MultiConsumer2);
            message2.PayloadType = nameof(MultiConsumerMessage);
            message2.Payload.Should().Be(serializedMessage);
        }
    }

    [Fact]
    public async Task ScheduledMessageProcessor_Increments_Available_After()
    {
        // Given
        var serviceProvider = await Setup();

        var consumerMessage = new MultiConsumerMessage
        {
            Name = "test-name"
        };

        using (var scope = serviceProvider.CreateScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<TestDbContext>();
            var scheduledMessageService =
                scope.ServiceProvider.GetRequiredService<ScheduledMessageService<TestDbContext>>();

            scheduledMessageService.Schedule(consumerMessage, 100, "test-tag");

            await dbContext.SaveChangesAsync();
        }

        FakeTime.SetUtcNow(FakeTime.GetUtcNow().AddSeconds(100));
        // When
        var processor = serviceProvider.GetRequiredService<ScheduledMessageProcessor<TestDbContext>>();

        var consumedMessage = await processor.ConsumeNext(CancellationToken.None);

        // Then
        consumedMessage.Should().BeTrue();
        using (var scope = serviceProvider.CreateScope())
        {
            var postDbContext = serviceProvider.GetRequiredService<TestDbContext>();
            var message = await postDbContext.ScheduledMessages.FirstOrDefaultAsync();
            message.Should().NotBeNull();
            message!.AvailableAfter.Should().Be(FakeTime.GetUtcNow().AddSeconds(100).ToUnixTimeSeconds());
        }
    }
}