using AsyncMonolith.Consumers;
using AsyncMonolith.Tests.Infra;
using AsyncMonolith.Utilities;
using Shouldly;
using Microsoft.Extensions.DependencyInjection;

namespace AsyncMonolith.Tests;

public class ConsumerRegistryTests
{
    private ServiceProvider Setup()
    {
        var services = new ServiceCollection();
        services.AddTestServices(DbType.Ef, AsyncMonolithSettings.Default);
        services.AddInMemoryDb();

        return services.BuildServiceProvider();
    }

    [Fact]
    public void ConsumerRegistry_Registers_Consumers()
    {
        // Given
        var serviceProvider = Setup();

        // When
        var testConsumer = serviceProvider.GetService<SingleConsumer>();

        // Then
        testConsumer.ShouldNotBeNull();
        testConsumer.ShouldBeOfType<SingleConsumer>();
    }

    [Fact]
    public void ConsumerRegistry_Resolves_ConsumerTimeout_By_Name()
    {
        // Given
        var serviceProvider = Setup();
        var registry = serviceProvider.GetRequiredService<ConsumerRegistry>();

        // When
        var timeout = registry.ResolveConsumerTimeout(nameof(SingleConsumer));

        // Then
        timeout.ShouldBe(1);
    }


    [Fact]
    public void ConsumerRegistry_Resolves_Default_ConsumerTimeout_When_Not_Set()
    {
        // Given
        var serviceProvider = Setup();
        var registry = serviceProvider.GetRequiredService<ConsumerRegistry>();

        // When
        var timeout = registry.ResolveConsumerTimeout(nameof(ExceptionConsumer));

        // Then
        timeout.ShouldBe(10);
    }

    [Fact]
    public void ConsumerRegistry_Resolves_ConsumerTimeout_By_ConsumerMessage()
    {
        // Given
        var serviceProvider = Setup();
        var registry = serviceProvider.GetRequiredService<ConsumerRegistry>();

        // When
        var timeout = registry.ResolveConsumerTimeout(new ConsumerMessage
        {
            ConsumerType = nameof(SingleConsumer),
            Id = default!,
            CreatedAt = default!,
            AvailableAfter = default!,
            PayloadType = default!,
            Payload = default!,
            Attempts = default,
            InsertId = string.Empty,
            TraceId = null,
            SpanId = null,
            RailId = 0
        });

        // Then
        timeout.ShouldBe(1);
    }

    [Fact]
    public void ConsumerRegistry_Resolves_ConsumerType()
    {
        // Given
        var serviceProvider = Setup();
        var registry = serviceProvider.GetRequiredService<ConsumerRegistry>();

        // When
        var consumerType = registry.ResolveConsumerType(new ConsumerMessage
        {
            ConsumerType = nameof(SingleConsumer),
            Id = default!,
            CreatedAt = default!,
            AvailableAfter = default!,
            PayloadType = default!,
            Payload = default!,
            Attempts = default,
            InsertId = string.Empty,
            TraceId = null,
            SpanId = null,
            RailId = 0
        });

        // Then
        consumerType.ShouldBe(typeof(SingleConsumer));
    }

    [Fact]
    public void ConsumerRegistry_Resolves_PayloadConsumerNames()
    {
        // Given
        var serviceProvider = Setup();
        var registry = serviceProvider.GetRequiredService<ConsumerRegistry>();

        // When
        var consumerIds = registry.ResolvePayloadConsumerTypes(nameof(MultiConsumerMessage));

        // Then
        consumerIds.Count.ShouldBe(2);
        Assert.Single(consumerIds.Where(c => c == nameof(MultiConsumer1)));
        Assert.Single(consumerIds.Where(c => c == nameof(MultiConsumer2)));
    }


    [Fact]
    public void ConsumerRegistry_Resolves_Default_ConsumerAttempts_When_Not_Set()
    {
        // Given
        var serviceProvider = Setup();
        var registry = serviceProvider.GetRequiredService<ConsumerRegistry>();

        // When
        var timeout = registry.ResolveConsumerMaxAttempts(new ConsumerMessage
        {
            ConsumerType = nameof(ExceptionConsumer),
            Id = default!,
            CreatedAt = default!,
            AvailableAfter = default!,
            PayloadType = default!,
            Payload = default!,
            Attempts = default,
            InsertId = string.Empty,
            TraceId = null,
            SpanId = null,
            RailId = 0
        });

        // Then
        timeout.ShouldBe(5);
    }

    [Fact]
    public void ConsumerRegistry_Resolves_ConsumerAttempts_By_ConsumerMessage()
    {
        // Given
        var serviceProvider = Setup();
        var registry = serviceProvider.GetRequiredService<ConsumerRegistry>();

        // When
        var timeout = registry.ResolveConsumerMaxAttempts(new ConsumerMessage
        {
            ConsumerType = nameof(SingleConsumer),
            Id = default!,
            CreatedAt = default!,
            AvailableAfter = default!,
            PayloadType = default!,
            Payload = default!,
            Attempts = default,
            InsertId = string.Empty,
            TraceId = null,
            SpanId = null,
            RailId = 0
        });

        // Then
        timeout.ShouldBe(2);
    }
}