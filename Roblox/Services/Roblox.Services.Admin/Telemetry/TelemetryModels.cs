namespace Roblox.Services.Admin.Telemetry;

public sealed record TelemetryPoint(DateTime Timestamp, double Value);
public sealed record TelemetrySeries(string Name, IReadOnlyList<TelemetryPoint> Points);
public sealed record TelemetryChart(string Key, string Title, string Unit, IReadOnlyList<TelemetrySeries> Series);

public sealed record TelemetrySummary(
    double? RequestRatePerSecond,
    double? ErrorRatePercent,
    double? P95RequestDurationMilliseconds,
    double? P95DatabaseDurationMilliseconds,
    double? CacheHitRatePercent,
    double? Signups,
    double? RobuxVolume);

public sealed record TelemetryDashboardResponse(
    DateTime GeneratedAt,
    string Range,
    int StepSeconds,
    string Service,
    IReadOnlyList<string> AvailableServices,
    TelemetrySummary Summary,
    IReadOnlyList<TelemetryChart> Charts,
    RenderPoolSnapshot? RenderPool = null);

public sealed record RenderPoolSnapshot(
    int WorkerCount,
    int IdleWorkerCount,
    int RunningJobs,
    int InteractiveQueuedJobs,
    int BackgroundQueuedJobs,
    int ConversionQueuedJobs,
    long ColdStarts,
    long ReusedWorkers,
    long Retries,
    long CoalescedRequests,
    double AverageQueueMilliseconds,
    double AverageRccMilliseconds,
    double AverageTotalMilliseconds,
    bool Ready);

public interface IRenderStatisticsClient
{
    Task<RenderPoolSnapshot?> GetAsync(CancellationToken cancellationToken);
}

public sealed class TelemetryQueryException : Exception
{
    public TelemetryQueryException(string message, Exception? innerException = null) : base(message, innerException) { }
}

public interface ITelemetryQueryService
{
    Task<TelemetryDashboardResponse> GetDashboardAsync(string range, string service, CancellationToken cancellationToken);
}
