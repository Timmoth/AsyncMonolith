using Cronos;

namespace AsyncMonolith.Scheduling;

/// <summary>Parses cron expressions with 5 fields (minute precision) or 6 fields (with seconds first).</summary>
internal static class Cron
{
    public static CronExpression Parse(string expression)
    {
        var fields = expression.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).Length;
        return CronExpression.Parse(expression, fields == 6 ? CronFormat.IncludeSeconds : CronFormat.Standard);
    }
}
