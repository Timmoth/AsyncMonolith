using AsnyMonolith.Consumers;
using AsnyMonolith.Producers;

namespace Demo.Controllers;

public class TotalValueConsumer : BaseConsumer<ValuePersisted>
{
    private readonly ProducerService<ApplicationDbContext> _producerService;
    public TotalValueConsumer(ProducerService<ApplicationDbContext> producerService)
    {
        _producerService = producerService;
    }

    public override Task Consume(ValuePersisted message, CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }
}