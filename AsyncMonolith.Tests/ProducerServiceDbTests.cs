using System.Text.Json;
using AsyncMonolith.Producers;
using AsyncMonolith.Tests.Infra;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace AsyncMonolith.Tests;

public class ProducerServiceDbTests : DbTestsBase
{
    [Theory]
    [InlineData(DbType.MySql)]
    [InlineData(DbType.PostgreSql)]
    public async Task Producer_Inserts_ConsumerMessage(DbType dbType)
    {
        var dbContainer = GetTestDbContainer(dbType);

        try
        {
            // Given
            var serviceProvider = await Setup(dbContainer);
            var producer = serviceProvider.GetRequiredService<ProducerService<TestDbContext>>();

            var delay = 10;
            var consumerMessage = new SingleConsumerMessage
            {
                Name = "test-name"
            };

            // When
            await producer.Produce(consumerMessage, delay);

            // Then
            using var scope = serviceProvider.CreateScope();
            {
                var postDbContext = serviceProvider.GetRequiredService<TestDbContext>();
                var messages = await postDbContext.ConsumerMessages.ToListAsync();
                var message = Assert.Single(messages);
                message.AvailableAfter.Should().Be(delay);
                message.Attempts.Should().Be(0);
                message.InsertId.Should().Be("fake-id-0");
                message.Id.Should().Be("fake-id-1");
                message.ConsumerType = nameof(SingleConsumer);
                message.PayloadType = nameof(SingleConsumerMessage);
                message.Payload.Should().Be(JsonSerializer.Serialize(consumerMessage));
            }
        }
        finally
        {
            await dbContainer.DisposeAsync();
        }
    }

    [Theory]
    [InlineData(DbType.MySql)]
    [InlineData(DbType.PostgreSql)]
    public async Task Producer_Inserts_List_Of_ConsumerMessages(DbType dbType)
    {
        var dbContainer = GetTestDbContainer(dbType);

        try
        {
            // Given
            var serviceProvider = await Setup(dbContainer);
            var producer = serviceProvider.GetRequiredService<ProducerService<TestDbContext>>();

            var delay = 10;
            var consumerMessage1 = new SingleConsumerMessage
            {
                Name = "test-name"
            };
            var consumerMessage2 = new SingleConsumerMessage
            {
                Name = "test-name-2"
            };
            var insertMessages = new List<SingleConsumerMessage>
            {
                consumerMessage1,
                consumerMessage2
            };

            // When
            await producer.ProduceList(insertMessages, delay);

            // Then
            using var scope = serviceProvider.CreateScope();
            {
                var postDbContext = serviceProvider.GetRequiredService<TestDbContext>();
                var messages = await postDbContext.ConsumerMessages.ToListAsync();
                messages.Count.Should().Be(2);

                var message1 = Assert.Single(messages.Where(m => m.Id == "fake-id-1"));
                message1.AvailableAfter.Should().Be(delay);
                message1.Attempts.Should().Be(0);
                message1.InsertId.Should().Be("fake-id-0");
                message1.Id.Should().Be("fake-id-1");
                message1.ConsumerType = nameof(SingleConsumer);
                message1.PayloadType = nameof(SingleConsumerMessage);
                message1.Payload.Should().Be(JsonSerializer.Serialize(consumerMessage1));

                var message2 = Assert.Single(messages.Where(m => m.Id == "fake-id-3"));
                message2.AvailableAfter.Should().Be(delay);
                message2.Attempts.Should().Be(0);
                message2.InsertId.Should().Be("fake-id-2");
                message2.Id.Should().Be("fake-id-3");
                message2.ConsumerType = nameof(SingleConsumer);
                message2.PayloadType = nameof(SingleConsumerMessage);
                message2.Payload.Should().Be(JsonSerializer.Serialize(consumerMessage2));
            }
        }
        finally
        {
            await dbContainer.DisposeAsync();
        }
    }

    [Fact]
    public async Task Ef_Producer_Inserts_List_Of_ConsumerMessages()
    {
        var dbContainer = GetTestDbContainer(DbType.Ef);

        try
        {
            // Given
            var serviceProvider = await Setup(dbContainer);
            var scope = serviceProvider.CreateScope();
            var producer = scope.ServiceProvider.GetRequiredService<ProducerService<TestDbContext>>();
            var dbContext = scope.ServiceProvider.GetRequiredService<TestDbContext>();

            var delay = 10;
            var consumerMessage1 = new SingleConsumerMessage
            {
                Name = "test-name"
            };
            var consumerMessage2 = new SingleConsumerMessage
            {
                Name = "test-name-2"
            };
            var insertMessages = new List<SingleConsumerMessage>
            {
                consumerMessage1,
                consumerMessage2
            };

            // When
            await producer.ProduceList(insertMessages, delay);
            await dbContext.SaveChangesAsync();

            // Then
            using var postScope = serviceProvider.CreateScope();
            {
                var postDbContext = postScope.ServiceProvider.GetRequiredService<TestDbContext>();
                var messages = await postDbContext.ConsumerMessages.ToListAsync();
                messages.Count.Should().Be(2);

                var message1 = Assert.Single(messages.Where(m => m.Id == "fake-id-1"));
                message1.AvailableAfter.Should().Be(delay);
                message1.Attempts.Should().Be(0);
                message1.InsertId.Should().Be("fake-id-0");
                message1.Id.Should().Be("fake-id-1");
                message1.ConsumerType = nameof(SingleConsumer);
                message1.PayloadType = nameof(SingleConsumerMessage);
                message1.Payload.Should().Be(JsonSerializer.Serialize(consumerMessage1));

                var message2 = Assert.Single(messages.Where(m => m.Id == "fake-id-3"));
                message2.AvailableAfter.Should().Be(delay);
                message2.Attempts.Should().Be(0);
                message2.InsertId.Should().Be("fake-id-2");
                message2.Id.Should().Be("fake-id-3");
                message2.ConsumerType = nameof(SingleConsumer);
                message2.PayloadType = nameof(SingleConsumerMessage);
                message2.Payload.Should().Be(JsonSerializer.Serialize(consumerMessage2));
            }
        }
        finally
        {
            await dbContainer.DisposeAsync();
        }
    }

    [Theory]
    [InlineData(DbType.MySql)]
    [InlineData(DbType.PostgreSql)]
    public async Task Producer_Does_Not_Insert_Duplicate_ConsumerMessage(DbType dbType)
    {
        var dbContainer = GetTestDbContainer(dbType);

        try
        {
            // Given
            var serviceProvider = await Setup(dbContainer);
            var producer = serviceProvider.GetRequiredService<ProducerService<TestDbContext>>();

            var delay = 10;
            var insertId = "test-insert_id";
            var consumerMessage = new SingleConsumerMessage
            {
                Name = "test-name"
            };
            await producer.Produce(consumerMessage, delay, insertId);

            // When
            await producer.Produce(new SingleConsumerMessage
            {
                Name = "test-name-2"
            }, delay, insertId);

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
                message.InsertId.Should().Be(insertId);
            }
        }
        finally
        {
            await dbContainer.DisposeAsync();
        }
    }

    [Theory]
    [InlineData(DbType.Ef)]
    public async Task EF_Producer_Throws_Exception_On_Duplicate_ConsumerMessage(DbType dbType)
    {
        var dbContainer = GetTestDbContainer(dbType);

        try
        {
            // Given
            var serviceProvider = await Setup(dbContainer);
            var scope = serviceProvider.CreateScope();
            var producer = scope.ServiceProvider.GetRequiredService<ProducerService<TestDbContext>>();
            var dbContext = scope.ServiceProvider.GetRequiredService<TestDbContext>();

            var delay = 10;
            var insertId = "test-insert_id";
            var consumerMessage = new SingleConsumerMessage
            {
                Name = "test-name"
            };
            await producer.Produce(consumerMessage, 10, insertId);

            // When
            await producer.Produce(new SingleConsumerMessage
            {
                Name = "test-name-2"
            }, delay, insertId);

            await Assert.ThrowsAsync<DbUpdateException>(async () => { await dbContext.SaveChangesAsync(); });
        }
        finally
        {
            await dbContainer.DisposeAsync();
        }
    }
}