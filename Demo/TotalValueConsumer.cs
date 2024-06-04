using AsnyMonolith.Consumers;
using Microsoft.EntityFrameworkCore;

namespace Demo;

public class TotalValueConsumer : BaseConsumer<ValuePersisted>
{
    private readonly TotalValueService _totalValueService;
    private readonly ApplicationDbContext _dbContext;
    public TotalValueConsumer(TotalValueService totalValueService, ApplicationDbContext dbContext)
    {
        _totalValueService = totalValueService;
        _dbContext = dbContext;
    }

    public override async Task Consume(ValuePersisted message, CancellationToken cancellationToken)
    {
        var totalValue = await _dbContext.SubmittedValues.SumAsync(v => v.Value, cancellationToken);
        _totalValueService.Set(totalValue);
    }
}