using Roblox.Rendering;

namespace Averia.RccServiceArbiter.Rendering;

public interface IRenderService
{
    Task<RenderOutput> RenderAsync(RenderRequest request, CancellationToken cancellationToken);
    RenderStatistics GetStatistics();
    int CleanUpIdleWorkers();
    bool IsReady { get; }
    Task EnsureWarmWorkersAsync(CancellationToken cancellationToken);
}

public sealed class RenderOutput
{
    public Guid JobId { get; init; }
    public string ContentType { get; init; } = "image/png";
    public byte[] Data { get; init; } = Array.Empty<byte>();
    public IReadOnlyList<string> DependencyUrls { get; init; } = Array.Empty<string>();
    public string WorkerState { get; init; } = "warm";
    public IReadOnlyDictionary<string, double> Timings { get; init; } = new Dictionary<string, double>();
}

public sealed class RenderStatistics
{
    public int WorkerCount { get; set; }
    public int IdleWorkerCount { get; set; }
    public int RunningJobs { get; set; }
    public int QueuedJobs { get; set; }
    public long CompletedJobs { get; set; }
    public long FailedJobs { get; set; }
    public int Capacity { get; set; }
    public int QueueCapacity { get; set; }
    public long ColdStarts { get; set; }
    public long ReusedWorkers { get; set; }
    public long CoalescedRequests { get; set; }
    public long Retries { get; set; }
    public long OutputBytes { get; set; }
    public int InteractiveQueuedJobs { get; set; }
    public int BackgroundQueuedJobs { get; set; }
    public int ConversionQueuedJobs { get; set; }
    public double AverageQueueMilliseconds { get; set; }
    public double AverageRccMilliseconds { get; set; }
    public double AverageTotalMilliseconds { get; set; }
    public bool Ready { get; set; }
}
