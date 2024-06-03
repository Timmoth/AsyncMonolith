using AsnyMonolith.Consumers;
using AsnyMonolith.Producers;
using AsyncMonolith.Tests.Infra;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Time.Testing;
using Testcontainers.PostgreSql;

namespace AsyncMonolith.Tests;

public class ConsumerMessageProcessorTests : IAsyncLifetime
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
    public async Task ConsumerMessageProcessor_Invokes_Consumer()
    {
        // Given
        var serviceProvider = await Setup();

        using (var scope = serviceProvider.CreateScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<TestDbContext>();
            var producer = scope.ServiceProvider.GetRequiredService<ProducerService<TestDbContext>>();

            producer.Produce(new SingleConsumerMessage
            {
                Name = "test-name"
            });

            await dbContext.SaveChangesAsync();
        }

        // When
        var processor = serviceProvider.GetRequiredService<ConsumerMessageProcessor<TestDbContext>>();

        var consumedMessage = await processor.ConsumeNext(CancellationToken.None);

        // Then
        consumedMessage.Should().BeTrue();
        _testConsumerInvocations.GetInvocationCount(nameof(SingleConsumer)).Should().Be(1);
    }

    [Fact]
    public async Task ConsumerMessageProcessor_Returns_False_If_No_Available_Messages()
    {
        // Given
        var serviceProvider = await Setup();

        using (var scope = serviceProvider.CreateScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<TestDbContext>();
            var producer = scope.ServiceProvider.GetRequiredService<ProducerService<TestDbContext>>();

            producer.Produce(new SingleConsumerMessage
            {
                Name = "test-name"
            }, FakeTime.GetUtcNow().ToUnixTimeSeconds() + 100);

            await dbContext.SaveChangesAsync();
        }

        var processor = serviceProvider.GetRequiredService<ConsumerMessageProcessor<TestDbContext>>();

        // When
        var consumedMessage = await processor.ConsumeNext(CancellationToken.None);

        // Then
        consumedMessage.Should().BeFalse();
        _testConsumerInvocations.GetInvocationCount(nameof(SingleConsumer)).Should().Be(0);
    }

    [Fact]
    public async Task ConsumerMessageProcessor_Returns_False_If_Only_Max_Attempted_Messages()
    {
        // Given
        var serviceProvider = await Setup();

        using (var scope = serviceProvider.CreateScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<TestDbContext>();
            var producer = scope.ServiceProvider.GetRequiredService<ProducerService<TestDbContext>>();

            producer.Produce(new SingleConsumerMessage
            {
                Name = "test-name"
            });

            await dbContext.SaveChangesAsync();

            var message = await dbContext.ConsumerMessages.FirstOrDefaultAsync();
            message!.Attempts = 6;
            await dbContext.SaveChangesAsync();
        }

        // When
        var processor = serviceProvider.GetRequiredService<ConsumerMessageProcessor<TestDbContext>>();

        var consumedMessage = await processor.ConsumeNext(CancellationToken.None);

        // Then
        consumedMessage.Should().BeFalse();
        _testConsumerInvocations.GetInvocationCount(nameof(SingleConsumer)).Should().Be(0);
    }

    [Fact]
    public async Task ConsumerMessageProcessor_Increments_Message_Attempts()
    {
        // Given
        var serviceProvider = await Setup();

        using (var scope = serviceProvider.CreateScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<TestDbContext>();
            var producer = scope.ServiceProvider.GetRequiredService<ProducerService<TestDbContext>>();

            producer.Produce(new ExceptionConsumerMessage
            {
                Name = "test-name"
            });

            await dbContext.SaveChangesAsync();
        }

        // When
        var processor = serviceProvider.GetRequiredService<ConsumerMessageProcessor<TestDbContext>>();

        await processor.ConsumeNext(CancellationToken.None);

        // Then
        using (var scope = serviceProvider.CreateScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<TestDbContext>();
            var message = await dbContext.ConsumerMessages.FirstOrDefaultAsync();
            message?.Attempts.Should().Be(1);
        }
    }

    [Fact]
    public async Task ConsumerMessageProcessor_Increments_Message_AvailableAfter()
    {
        // Given
        var serviceProvider = await Setup();

        using (var scope = serviceProvider.CreateScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<TestDbContext>();
            var producer = scope.ServiceProvider.GetRequiredService<ProducerService<TestDbContext>>();

            producer.Produce(new ExceptionConsumerMessage
            {
                Name = "test-name"
            });

            await dbContext.SaveChangesAsync();
        }

        // When
        var processor = serviceProvider.GetRequiredService<ConsumerMessageProcessor<TestDbContext>>();

        await processor.ConsumeNext(CancellationToken.None);

        // Then
        using (var scope = serviceProvider.CreateScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<TestDbContext>();
            var message = await dbContext.ConsumerMessages.FirstOrDefaultAsync();
            message?.AvailableAfter.Should().Be(FakeTime.GetUtcNow().ToUnixTimeSeconds() + 10);
        }
    }

    [Fact]
    public async Task ConsumerMessageProcessor_Removes_Consumed_Messages()
    {
        // Given
        var serviceProvider = await Setup();

        var preMessageCount = 0;
        using (var scope = serviceProvider.CreateScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<TestDbContext>();
            var producer = scope.ServiceProvider.GetRequiredService<ProducerService<TestDbContext>>();

            producer.Produce(new SingleConsumerMessage
            {
                Name = "test-name"
            });

            await dbContext.SaveChangesAsync();

            preMessageCount = await dbContext.ConsumerMessages.CountAsync();
        }

        // When
        var processor = serviceProvider.GetRequiredService<ConsumerMessageProcessor<TestDbContext>>();

        var consumedMessage = await processor.ConsumeNext(CancellationToken.None);

        // Then
        consumedMessage.Should().BeTrue();

        using (var scope = serviceProvider.CreateScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<TestDbContext>();
            var remaining = await dbContext.ConsumerMessages.CountAsync();
            remaining.Should().Be(preMessageCount - 1);
        }
    }
}