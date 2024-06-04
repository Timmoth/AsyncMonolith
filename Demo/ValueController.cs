using AsyncMonolith.Producers;
using Microsoft.AspNetCore.Mvc;

namespace Demo;

[ApiController]
[Route("api/values")]
public class ValueController : ControllerBase
{
    private readonly ApplicationDbContext _dbContext;
    private readonly ProducerService<ApplicationDbContext> _producerService;
    private readonly TotalValueService _totalValueService;

    public ValueController(ProducerService<ApplicationDbContext> producerService, ApplicationDbContext dbContext,
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

        _producerService.Produce(new ValueSubmitted
        {
            Value = newValue
        });
        await _dbContext.SaveChangesAsync(cancellationToken);

        return Ok(sum + newValue);
    }
}