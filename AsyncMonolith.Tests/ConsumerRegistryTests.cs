using AsyncMonolith.Consumers;
using AsyncMonolith.Tests.Infra;
using AsyncMonolith.Utilities;
using FluentAssertions;
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
        testConsumer.Should().NotBeNull();
        testConsumer.Should().BeOfType<SingleConsumer>();
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
            InsertId = string.Empty
        });

        // Then
        consumerType.Should().Be(typeof(SingleConsumer));
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
        consumerIds.Count.Should().Be(2);
        Assert.Single(consumerIds.Where(c => c == nameof(MultiConsumer1)));
        Assert.Single(consumerIds.Where(c => c == nameof(MultiConsumer2)));
    }
}