using System.Text.Json;
using AsnyMonolith.Consumers;
using AsnyMonolith.Scheduling;
using AsyncMonolith.Tests.Infra;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Time.Testing;
using Testcontainers.PostgreSql;

namespace AsyncMonolith.Tests;

public class ScheduledMessageServiceTests : IAsyncLifetime
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

        services.AddSingleton<ConsumerMessageProcessor<TestDbContext>>();
        var serviceProvider = services.BuildServiceProvider();

        using var scope = serviceProvider.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<TestDbContext>();
        await dbContext.Database.EnsureCreatedAsync();

        await dbContext.SaveChangesAsync();

        return serviceProvider;
    }


    [Fact]
    public async Task Schedule_Writes_Scheduled_Message()
    {
        // Given
        var serviceProvider = await Setup();
        var consumerMessage = new SingleConsumerMessage
        {
            Name = "test-name"
        };

        var delay = 100;
        var scheduledMessageService = serviceProvider.GetRequiredService<ScheduledMessageService<TestDbContext>>();
        var dbContext = serviceProvider.GetRequiredService<TestDbContext>();
        var tags = new[] { "test-tag" };
        // When
        scheduledMessageService.Schedule(consumerMessage, delay, tags);
        await dbContext.SaveChangesAsync();

        // Then
        using var scope = serviceProvider.CreateScope();
        {
            var postDbContext = serviceProvider.GetRequiredService<TestDbContext>();
            var messages = await postDbContext.ScheduledMessages.ToListAsync();
            var message = Assert.Single(messages);
            message.AvailableAfter.Should().Be(FakeTime.GetUtcNow().ToUnixTimeSeconds() + delay);
            message.Id.Should().Be("fake-id-0");
            message.Tags.Should().BeEquivalentTo(tags);
            message.PayloadType = nameof(SingleConsumerMessage);
            message.Payload.Should().Be(JsonSerializer.Serialize(consumerMessage));
        }
    }

    [Fact]
    public async Task DeleteByTag_Deletes_Scheduled_Messages()
    {
        // Given
        var serviceProvider = await Setup();
        var consumerMessage1 = new SingleConsumerMessage
        {
            Name = "test-name"
        };
        var consumerMessage2 = new MultiConsumerMessage
        {
            Name = "test-name"
        };

        var delay = 100;
        var scheduledMessageService = serviceProvider.GetRequiredService<ScheduledMessageService<TestDbContext>>();
        var dbContext = serviceProvider.GetRequiredService<TestDbContext>();
        var tags = new[] { "test-tag" };
        scheduledMessageService.Schedule(consumerMessage1, delay, tags);
        scheduledMessageService.Schedule(consumerMessage2, delay, tags);
        await dbContext.SaveChangesAsync();

        // When
        await scheduledMessageService.DeleteByTag("test-tag", CancellationToken.None);
        await dbContext.SaveChangesAsync();

        // Then
        using var scope = serviceProvider.CreateScope();
        {
            var postDbContext = serviceProvider.GetRequiredService<TestDbContext>();
            var count = await postDbContext.ScheduledMessages.CountAsync();
            count.Should().Be(0);
        }
    }


    [Fact]
    public async Task DeleteById_Deletes_Scheduled_Messages()
    {
        // Given
        var serviceProvider = await Setup();
        var consumerMessage = new SingleConsumerMessage
        {
            Name = "test-name"
        };

        var delay = 100;
        var scheduledMessageService = serviceProvider.GetRequiredService<ScheduledMessageService<TestDbContext>>();
        var dbContext = serviceProvider.GetRequiredService<TestDbContext>();
        var id = scheduledMessageService.Schedule(consumerMessage, delay);
        await dbContext.SaveChangesAsync();

        // When
        await scheduledMessageService.DeleteById(id, CancellationToken.None);
        await dbContext.SaveChangesAsync();

        // Then
        using var scope = serviceProvider.CreateScope();
        {
            var postDbContext = serviceProvider.GetRequiredService<TestDbContext>();
            var count = await postDbContext.ScheduledMessages.CountAsync();
            count.Should().Be(0);
        }
    }
}