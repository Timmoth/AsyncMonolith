using AsyncMonolith.Utilities;
using Shouldly;

namespace AsyncMonolith.Tests;

/// <summary>
///     How long processors wait between batches.
/// </summary>
public class PollingTests
{
    private static readonly AsyncMonolithOptions Options = new()
    {
        BatchSize = 5,
        IdlePollInterval = TimeSpan.FromSeconds(1),
        BusyPollInterval = TimeSpan.FromMilliseconds(10)
    };

    [Theory]
    [InlineData(0, 1000)]
    [InlineData(4, 1000)]
    [InlineData(5, 10)]
    public void A_full_batch_polls_again_quickly(int processed, int expectedMilliseconds)
    {
        ProcessorPolling.NextDelay(Options, processed, 0).ShouldBe(TimeSpan.FromMilliseconds(expectedMilliseconds));
    }

    [Theory]
    [InlineData(1, 2)]
    [InlineData(2, 4)]
    [InlineData(3, 8)]
    [InlineData(4, 16)]
    [InlineData(5, 30)]
    [InlineData(1000, 30)]
    public void Repeated_failures_back_off_up_to_thirty_seconds(int failures, int expectedSeconds)
    {
        ProcessorPolling.NextDelay(Options, 0, failures).ShouldBe(TimeSpan.FromSeconds(expectedSeconds));
    }
}
