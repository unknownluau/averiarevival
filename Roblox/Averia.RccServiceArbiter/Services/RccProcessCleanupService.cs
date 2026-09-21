using Averia.RccServiceArbiter.Configuration;
using Averia.RccServiceArbiter.Processes;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Averia.RccServiceArbiter.Services;

public sealed class RccProcessCleanupService : BackgroundService
{
    private readonly IRccProcessPool _pool;
    private readonly ArbiterOptions _options;
    private readonly ILogger<RccProcessCleanupService> _logger;

    public RccProcessCleanupService(
        IRccProcessPool pool,
        IOptions<ArbiterOptions> options,
        ILogger<RccProcessCleanupService> logger)
    {
        _pool = pool;
        _options = options.Value;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await Task.Delay(TimeSpan.FromSeconds(_options.Processes.CleanupIntervalSeconds), stoppingToken);
                var removed = await _pool.CleanUpAsync(stoppingToken);
                if (removed > 0)
                {
                    _logger.LogInformation("Cleaned up {Count} RCC process handles", removed);
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                return;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "RCC process cleanup failed");
            }
        }
    }
}
