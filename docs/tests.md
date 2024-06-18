## AsyncMonolith.Tests

- Some of the test rely on TestContainers to run against real databases, make sure you've got docker installed


## AsyncMonolith.TestHelpers

[![NuGet](https://img.shields.io/nuget/v/AsyncMonolith.TestHelpers)](https://www.nuget.org/packages/AsyncMonolith.TestHelpers)

Install the TestHelpers package to help with unit / integration tests.

### FakeIdGenerator

Generates sequential fake id's of the format `$"fake-id-{invocationCount}"`

### ConsumerMessageTestHelpers

Static methods for asserting messages have been inserted into the `consumer_messages` table.

### FakeProducerService

Provides a fake `IProducerService` implementation, which is useful for asserting messages have been published without using a database.

### FakeScheduleService

Provides a fake `IScheduleService` implementation, which is useful for asserting messages have been scheduled without using a database.

### SetupTestHelpers

Includes two methods to configure your `IServiceCollection` without including the background services for processing messages. 

- `AddFakeAsyncMonolithBaseServices` configures fake services which don't depend on a database
- `AddRealAsyncMonolithBaseServices` configrues real services without registering the background processors

### TestConsumerMessageProcessor

Static methods for invoking and testing your consumers.

### ConsumerTestBase

The `ConsumerTestBase` offers a way to write simple tests for your consumers

```cs
public class CancelShipmentTests : ConsumerTestBase
{
    public CancelShipmentTests(ITestOutputHelper testOutputHelper) : base(testOutputHelper)
    {
    }
    private readonly string _inMemoryDatabaseName = Guid.NewGuid().ToString();
    protected override Task Setup(IServiceCollection services)
    {
        services.AddDbContext<ApplicationDbContext>((sp, options) =>
            {
                options.UseInMemoryDatabase(_inMemoryDatabaseName);
            }
        );

        services.AddRealAsyncMonolithBaseServices<ApplicationDbContext>(typeof(Program).Assembly, AsyncMonolithSettings.Default);
        return Task.CompletedTask;
    }

    [Fact]
    public async Task OrderCancelled_Sets_Shipment_Status_To_Cancelled()
    {
        // Given
        var model = new Shipment
        {
            Id = "test-shipment-id",
            OrderId = "test-order-id",
            Status = "pending",
        };

        using (var scope = Services.CreateScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            dbContext.Shipments.Add(model);
            await dbContext.SaveChangesAsync();
        }

        // When
        await Process<CancelShipmentConsumer, OrderCancelled>(new OrderCancelled()
        {
            OrderId = model.OrderId
        });

        // Then
        using (var scope = Services.CreateScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var shipment = await dbContext.Shipments.FirstOrDefaultAsync(c => c.Id == model.Id);
            shipment.Status.Should().Be("cancelled");
            await dbContext.SaveChangesAsync();
        }
    }
}
```