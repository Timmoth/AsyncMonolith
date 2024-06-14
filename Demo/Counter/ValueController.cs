using AsyncMonolith.Producers;
using Microsoft.AspNetCore.Mvc;

namespace Demo.Counter;

[ApiController]
[Route("api/values")]
public class ValueController : ControllerBase
{
    private readonly ApplicationDbContext _dbContext;
    private readonly IProducerService _producerService;
    private readonly TotalValueService _totalValueService;

    public ValueController(IProducerService producerService, ApplicationDbContext dbContext,
        TotalValueService totalValueService)
    {
        _producerService = producerService;
        _dbContext = dbContext;
        _totalValueService = totalValueService;
    }

    [HttpGet]
    public async Task<IActionResult> Get(CancellationToken cancellationToken)
    {
        var newValue = 1;
        var sum = _totalValueService.Get();

        await _producerService.Produce(new ValueSubmitted
        {
            Value = newValue
        });
        await _dbContext.SaveChangesAsync(cancellationToken);

        return Ok(sum + newValue);
    }
}