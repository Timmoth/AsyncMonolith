using AsyncMonolith.Producers;
using Microsoft.AspNetCore.Mvc;

namespace Demo.Spam;

[ApiController]
[Route("api/spam")]
public class SpamController : ControllerBase
{
    private readonly ApplicationDbContext _dbContext;
    private readonly ProducerService<ApplicationDbContext> _producerService;
    private readonly TimeProvider _timeProvider;
    public SpamController(ProducerService<ApplicationDbContext> producerService, ApplicationDbContext dbContext, TimeProvider timeProvider)
    {
        _producerService = producerService;
        _dbContext = dbContext;
        _timeProvider = timeProvider;
    }

    [HttpGet]
    public async Task<IActionResult> Spam(CancellationToken cancellationToken)
    {
        var count = 1000;
        if (SpamResultService.Start != null && SpamResultService.End == null)
        {
            var duration = _timeProvider.GetUtcNow().ToUnixTimeMilliseconds() - SpamResultService.Start;
            return Ok($"Running. consumed: {SpamResultService.Count} / {count}. {duration / (SpamResultService.Count + 1)}ms per message");
        }

        if (SpamResultService.Start != null && SpamResultService.End != null)
        {
            var duration = SpamResultService.End - SpamResultService.Start;
            SpamResultService.Start = null;
            SpamResultService.End = null;
            return Ok($"Finished consumed: {SpamResultService.Count} / {count}. {duration / (SpamResultService.Count + 1)}ms per message");
        }

        SpamResultService.Count = 0;
        for (var i = 0; i < count - 1; i++)
        {
            _producerService.Produce(new SpamMessage()
            {
                Last = false,
            });
        }
        await _dbContext.SaveChangesAsync(cancellationToken);
        SpamResultService.Start = _timeProvider.GetUtcNow().ToUnixTimeMilliseconds();

        await Task.Delay(1000, cancellationToken);
        _producerService.Produce(new SpamMessage()
        {
            Last = true,
        });

        await _dbContext.SaveChangesAsync(cancellationToken);

        return Ok("Started.");
    }

}