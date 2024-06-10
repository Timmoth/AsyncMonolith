using AsyncMonolith.Producers;
using Microsoft.AspNetCore.Mvc;

namespace Demo.Spam;

[ApiController]
[Route("api/spam")]
public class SpamController : ControllerBase
{
    private readonly ProducerService<ApplicationDbContext> _producerService;
    private readonly TimeProvider _timeProvider;

    public SpamController(ProducerService<ApplicationDbContext> producerService, TimeProvider timeProvider)
    {
        _producerService = producerService;
        _timeProvider = timeProvider;
    }

    [HttpGet]
    public async Task<IActionResult> Spam([FromQuery(Name = "count")] int count, CancellationToken cancellationToken)
    {
        if (count <= 0) return BadRequest("'count' query parameter must be at least 1");
        if (SpamResultService.Start != null && SpamResultService.End == null)
        {
            var duration = _timeProvider.GetUtcNow().ToUnixTimeMilliseconds() - SpamResultService.Start;
            return Ok(
                $"Running. consumed: {SpamResultService.Count} / {count}. {duration / (SpamResultService.Count + 1)}ms per message");
        }

        if (SpamResultService.Start != null && SpamResultService.End != null)
        {
            var duration = SpamResultService.End - SpamResultService.Start;
            SpamResultService.Start = null;
            SpamResultService.End = null;
            return Ok(
                $"Finished consumed: {SpamResultService.Count} / {count}. {duration / (SpamResultService.Count + 1)}ms per message");
        }

        SpamResultService.Count = 0;

        var messages = new List<SpamMessage>();
        for (var i = 0; i < count - 1; i++)
            messages.Add(new SpamMessage
            {
                Last = false
            });

        await _producerService.ProduceList(messages);
        SpamResultService.Start = _timeProvider.GetUtcNow().ToUnixTimeMilliseconds();

        await _producerService.Produce(new SpamMessage
        {
            Last = true
        }, 10);

        return Ok("Started.");
    }
}