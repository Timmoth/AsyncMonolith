using AsnyMonolith.Producers;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Demo;

[ApiController]
[Route("api/values")]
public class ValueController : ControllerBase
{
    private readonly ApplicationDbContext _dbContext;
    private readonly ProducerService<ApplicationDbContext> _producerService;

    public ValueController(ProducerService<ApplicationDbContext> producerService, ApplicationDbContext dbContext)
    {
        _producerService = producerService;
        _dbContext = dbContext;
    }

    [HttpGet]
    public async Task<IActionResult> Get(CancellationToken cancellationToken)
    {
        var newValue = Random.Shared.NextDouble() * 100;
        var sum = await _dbContext.SubmittedValues.SumAsync(v => v.Value, cancellationToken);

        _producerService.Produce(new ValueSubmitted
        {
            Value = newValue
        });
        await _dbContext.SaveChangesAsync(cancellationToken);

        return Ok(sum + newValue);
    }
}