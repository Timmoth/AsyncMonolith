using System.Diagnostics;
using System.Text.Json;
using AsyncMonolith.Producers;
using AsyncMonolith.TestHelpers;
using AsyncMonolith.Tests.Infra;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace AsyncMonolith.Tests;

public class ProducerServiceDbTests : DbTestsBase
{
    [Theory]
    [InlineData(DbType.MySql)]
    [InlineData(DbType.MsSql)]
    [InlineData(DbType.PostgreSql)]
    [InlineData(DbType.MariaDb)]
    public async Task Producer_Inserts_ConsumerMessage(DbType dbType)
    {
        var dbContainer = GetTestDbContainer(dbType);

        try
        {
            // Given
            var serviceProvider = await Setup(dbContainer);
            var producer = serviceProvider.GetRequiredService<IProducerService>();

            var delay = 10;
            var consumerMessage = new SingleConsumerMessage
            {
                Name = "test-name"
            };
            using var activity = GetActivity();
            Activity.Current = activity;

            // When
            await producer.Produce(consumerMessage, delay);

            // Then
            using var scope = serviceProvider.CreateScope();
            {
                var postDbContext = serviceProvider.GetRequiredService<TestDbContext>();
                var message =
                    await postDbContext.AssertSingleConsumerMessage<SingleConsumer, SingleConsumerMessage>(
                        consumerMessage);
                message.AvailableAfter.Should().Be(delay);
                message.Attempts.Should().Be(0);
                message.InsertId.Should().Be("fake-id-0");
                message.Id.Should().Be("fake-id-1");
                message.ConsumerType = nameof(SingleConsumer);
                message.PayloadType = nameof(SingleConsumerMessage);
                message.Payload.Should().Be(JsonSerializer.Serialize(consumerMessage));
                message.TraceId.Should().Be(activity?.TraceId.ToString());
                message.SpanId.Should().Be(activity?.SpanId.ToString());
            }
        }
        finally
        {
            await dbContainer.DisposeAsync();
        }
    }

    [Theory]
    [InlineData(DbType.MySql)]
    [InlineData(DbType.MsSql)]
    [InlineData(DbType.PostgreSql)]
    [InlineData(DbType.MariaDb)]
    public async Task Producer_Inserts_List_Of_ConsumerMessages(DbType dbType)
    {
        var dbContainer = GetTestDbContainer(dbType);

        try
        {
            // Given
            var serviceProvider = await Setup(dbContainer);
            var producer = serviceProvider.GetRequiredService<IProducerService>();

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
            using var activity = GetActivity();
            Activity.Current = activity;

            // When
            await producer.ProduceList(insertMessages, delay);

            // Then
            using var scope = serviceProvider.CreateScope();
            {
                var postDbContext = serviceProvider.GetRequiredService<TestDbContext>();
                var messages = await postDbContext.ConsumerMessages.ToListAsync();
                messages.Count.Should().Be(2);

                var message1 =
                    await postDbContext.AssertSingleConsumerMessageById<SingleConsumer, SingleConsumerMessage>(
                        consumerMessage1, "fake-id-1");
                message1.AvailableAfter.Should().Be(delay);
                message1.Attempts.Should().Be(0);
                message1.InsertId.Should().Be("fake-id-0");
                message1.Id.Should().Be("fake-id-1");
                message1.ConsumerType = nameof(SingleConsumer);
                message1.PayloadType = nameof(SingleConsumerMessage);
                message1.Payload.Should().Be(JsonSerializer.Serialize(consumerMessage1));
                message1.TraceId.Should().Be(activity?.TraceId.ToString());
                message1.SpanId.Should().Be(activity?.SpanId.ToString());

                var message2 =
                    await postDbContext.AssertSingleConsumerMessageById<SingleConsumer, SingleConsumerMessage>(
                        consumerMessage2, "fake-id-3");
                message2.AvailableAfter.Should().Be(delay);
                message2.Attempts.Should().Be(0);
                message2.InsertId.Should().Be("fake-id-2");
                message2.Id.Should().Be("fake-id-3");
                message2.ConsumerType = nameof(SingleConsumer);
                message2.PayloadType = nameof(SingleConsumerMessage);
                message2.Payload.Should().Be(JsonSerializer.Serialize(consumerMessage2));
                message2.TraceId.Should().Be(activity?.TraceId.ToString());
                message2.SpanId.Should().Be(activity?.SpanId.ToString());
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
            var producer = scope.ServiceProvider.GetRequiredService<IProducerService>();
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
            using var activity = GetActivity();
            Activity.Current = activity;

            // When
            await producer.ProduceList(insertMessages, delay);
            await dbContext.SaveChangesAsync();

            // Then
            using var postScope = serviceProvider.CreateScope();
            {
                var postDbContext = postScope.ServiceProvider.GetRequiredService<TestDbContext>();
                var messages = await postDbContext.ConsumerMessages.ToListAsync();
                messages.Count.Should().Be(2);

                var message1 =
                    await postDbContext.AssertSingleConsumerMessageById<SingleConsumer, SingleConsumerMessage>(
                        consumerMessage1, "fake-id-1");
                message1.AvailableAfter.Should().Be(delay);
                message1.Attempts.Should().Be(0);
                message1.InsertId.Should().Be("fake-id-0");
                message1.Id.Should().Be("fake-id-1");
                message1.ConsumerType = nameof(SingleConsumer);
                message1.PayloadType = nameof(SingleConsumerMessage);
                message1.Payload.Should().Be(JsonSerializer.Serialize(consumerMessage1));
                message1.TraceId.Should().Be(activity?.TraceId.ToString());
                message1.SpanId.Should().Be(activity?.SpanId.ToString());
                var message2 =
                    await postDbContext.AssertSingleConsumerMessageById<SingleConsumer, SingleConsumerMessage>(
                        consumerMessage2, "fake-id-3");
                message2.AvailableAfter.Should().Be(delay);
                message2.Attempts.Should().Be(0);
                message2.InsertId.Should().Be("fake-id-2");
                message2.Id.Should().Be("fake-id-3");
                message2.ConsumerType = nameof(SingleConsumer);
                message2.PayloadType = nameof(SingleConsumerMessage);
                message2.Payload.Should().Be(JsonSerializer.Serialize(consumerMessage2));
                message2.TraceId.Should().Be(activity?.TraceId.ToString());
                message2.SpanId.Should().Be(activity?.SpanId.ToString());
            }
        }
        finally
        {
            await dbContainer.DisposeAsync();
        }
    }

    [Theory]
    [InlineData(DbType.MySql)]
    [InlineData(DbType.MsSql)]
    [InlineData(DbType.PostgreSql)]
    [InlineData(DbType.MariaDb)]
    public async Task Producer_Does_Not_Insert_Duplicate_ConsumerMessage(DbType dbType)
    {
        var dbContainer = GetTestDbContainer(dbType);

        try
        {
            // Given
            var serviceProvider = await Setup(dbContainer);
            var producer = serviceProvider.GetRequiredService<IProducerService>();

            var delay = 10;
            var insertId = "test-insert_id";
            var consumerMessage = new SingleConsumerMessage
            {
                Name = "test-name"
            };
            using var activity = GetActivity();
            Activity.Current = activity;

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
                var message =
                    await postDbContext.AssertSingleConsumerMessage<SingleConsumer, SingleConsumerMessage>(
                        consumerMessage);
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
            var producer = scope.ServiceProvider.GetRequiredService<IProducerService>();
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