using Averia.RccServiceArbiter.Configuration;
using Averia.RccServiceArbiter.Rendering;
using Microsoft.Extensions.Options;

namespace Averia.RccServiceArbiter.Services;

public sealed class RenderWorkerCleanupService(IRenderService renderer, IOptions<ArbiterOptions> options,
    ILogger<RenderWorkerCleanupService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(options.Value.Processes.CleanupIntervalSeconds));
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                renderer.CleanUpIdleWorkers();
                await renderer.EnsureWarmWorkersAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
            catch (Exception ex) { logger.LogError(ex, "Failed to maintain the RCC render warm pool"); }
            if (!await timer.WaitForNextTickAsync(stoppingToken)) break;
        }
    }
}
