using AsyncMonolith.Consumers;
using Microsoft.EntityFrameworkCore;

namespace Example.Api.Counter;

[ConsumerExecutionMode(ConsumerInstanceExecutionMode.Parallel)]
public class TotalValueConsumer : IConsumer<ValuePersisted>
{
    private readonly ApplicationDbContext _dbContext;
    private readonly TotalValueService _totalValueService;

    public TotalValueConsumer(TotalValueService totalValueService, ApplicationDbContext dbContext)
    {
        _totalValueService = totalValueService;
        _dbContext = dbContext;
    }

    public async Task Consume(ValuePersisted message, ConsumeContext context, CancellationToken cancellationToken)
    {
        var totalValue = await _dbContext.SubmittedValues.SumAsync(v => v.Value, cancellationToken);
        _totalValueService.Set(totalValue);
        await _dbContext.SaveChangesAsync(cancellationToken);
    }
}