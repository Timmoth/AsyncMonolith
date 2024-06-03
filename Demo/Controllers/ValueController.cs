using AsnyMonolith.Producers;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Demo.Controllers
{
    [ApiController]
    [Route("api/values")]
    public class ValueController : ControllerBase
    {
        private readonly ProducerService<ApplicationDbContext> _producerService;
        private readonly ApplicationDbContext _dbContext;
        public ValueController(ProducerService<ApplicationDbContext> producerService, ApplicationDbContext dbContext)
        {
            _producerService = producerService;
            _dbContext = dbContext;
        }

        [HttpGet]
        public async Task<IActionResult> Get(CancellationToken cancellationToken)
        {
            var newValue = Random.Shared.NextDouble() * 100;
            var sum = await _dbContext.SubmittedValues.SumAsync(v => v.Value, cancellationToken: cancellationToken);
           
            _producerService.Produce(new ValueSubmitted()
            {
                Value = newValue
            });
            await _dbContext.SaveChangesAsync(cancellationToken);

            return Ok(sum + newValue);
        }
    }
}
