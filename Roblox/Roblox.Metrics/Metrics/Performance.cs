using System.Diagnostics;

namespace Roblox.Metrics;

public static class PerformanceMetrics
{
    public static void ReportRedisLookup(string prefix, string layer, bool hit)
    {
        RobloxMetrics.CacheLookups.Add(1,
            new KeyValuePair<string, object?>("cache.prefix", Normalize(prefix)),
            new KeyValuePair<string, object?>("cache.layer", Normalize(layer)),
            new KeyValuePair<string, object?>("cache.result", hit ? "hit" : "miss"));
    }

    public static void ReportDbDuration(string operation, long elapsedMilliseconds, bool slow)
    {
        Debug.Assert(elapsedMilliseconds >= 0);
        RobloxMetrics.DatabaseDuration.Record(elapsedMilliseconds,
            new KeyValuePair<string, object?>("db.operation", Normalize(operation)),
            new KeyValuePair<string, object?>("db.slow", slow));
    }

    private static string Normalize(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return "unknown";
        var trimmed = value.Trim().ToLowerInvariant();
        return trimmed.Length <= 64 ? trimmed : "other";
    }
}
