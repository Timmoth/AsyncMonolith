using AsyncMonolith.Consumers;
using AsyncMonolith.Producers;
using Demo.Spam;

namespace Demo.Counter;

public class ValueSubmittedConsumer : BaseConsumer<ValueSubmitted>
{
    private readonly ApplicationDbContext _dbContext;
    private readonly ProducerService<ApplicationDbContext> _producerService;

    public ValueSubmittedConsumer(ApplicationDbContext dbContext, ProducerService<ApplicationDbContext> producerService)
    {
        _dbContext = dbContext;
        _producerService = producerService;
    }

    public override async Task Consume(ValueSubmitted message, CancellationToken cancellationToken)
    {
        var newValue = new SubmittedValue
        {
            Value = message.Value
        };

        _dbContext.SubmittedValues.Add(newValue);
        _producerService.Produce(new ValuePersisted());
        await _dbContext.SaveChangesAsync(cancellationToken);
    }
}