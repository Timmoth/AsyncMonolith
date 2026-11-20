namespace AsyncMonolith.Utilities;

/// <summary>How long a processor waits before its next batch.</summary>
internal static class ProcessorPolling
{
    /// <summary>The longest wait between attempts while batches keep failing (for example during an outage).</summary>
    public static readonly TimeSpan MaxFailureDelay = TimeSpan.FromSeconds(30);

    /// <param name="options">The options.</param>
    /// <param name="processed">How many items the last batch handled.</param>
    /// <param name="consecutiveFailures">How many batches in a row have thrown, 0 if the last one succeeded.</param>
    public static TimeSpan NextDelay(AsyncMonolithOptions options, int processed, int consecutiveFailures)
    {
        if (consecutiveFailures > 0)
        {
            var delay = options.IdlePollInterval;
            for (var i = 0; i < consecutiveFailures && delay < MaxFailureDelay; i++)
            {
                delay *= 2;
            }

            return delay < MaxFailureDelay ? delay : MaxFailureDelay;
        }

        return processed >= options.BatchSize ? options.BusyPollInterval : options.IdlePollInterval;
    }
}
