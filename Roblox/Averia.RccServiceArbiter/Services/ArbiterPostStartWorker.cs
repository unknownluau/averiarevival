using Averia.RccServiceArbiter.Configuration;
using Averia.RccServiceArbiter.Processes;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Averia.RccServiceArbiter.Services;

public sealed class ArbiterPostStartWorker : BackgroundService
{
    private readonly IArbiterPostStartQueue _queue;
    private readonly IRccProcessPool _pool;
    private readonly ArbiterOptions _options;
    private readonly ILogger<ArbiterPostStartWorker> _logger;

    public ArbiterPostStartWorker(
        IArbiterPostStartQueue queue,
        IRccProcessPool pool,
        IOptions<ArbiterOptions> options,
        ILogger<ArbiterPostStartWorker> logger)
    {
        _queue = queue;
        _pool = pool;
        _options = options.Value;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await foreach (var action in _queue.ReadAllAsync(stoppingToken))
        {
            try
            {
                if (_options.PostStartDelaySeconds > 0)
                {
                    await Task.Delay(TimeSpan.FromSeconds(_options.PostStartDelaySeconds), stoppingToken);
                }

                if (action.Year >= 2020 && !string.IsNullOrWhiteSpace(_options.GlobalMessageTopic))
                {
                    await _pool.RunGlobalMessageAsync(action.JobId, _options.GlobalMessageTopic, stoppingToken);
                }
                else if (_options.ForcedFilteringEnabled)
                {
                    await _pool.SetFilteringEnabledAsync(action.JobId, true, stoppingToken);
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Post-start action failed for job {JobId}", action.JobId);
            }
        }
    }
}
