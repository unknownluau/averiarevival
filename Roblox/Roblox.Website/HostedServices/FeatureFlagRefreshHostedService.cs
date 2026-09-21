using System.Diagnostics;
using Roblox.Logging;
using Roblox.Services.App.FeatureFlags;

namespace Roblox.Website.HostedServices;

public class FeatureFlagRefreshHostedService : BackgroundService
{
    private readonly ILogger<FeatureFlagRefreshHostedService> _logger;

    public FeatureFlagRefreshHostedService(ILogger<FeatureFlagRefreshHostedService> logger)
    {
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var failureCount = 0;

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await FeatureFlags.RefreshOnceAsync();
                failureCount = 0;
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception e)
            {
                failureCount++;
                _logger.LogError(e, "Error updating feature flags. Process will crash after 5 failures.");
                Writer.Info(LogGroup.FeatureFlags,
                    "Error updating flags. Process will crash after 5 failures. Error = {0}", e.Message);
            }

            if (failureCount >= 5)
            {
                _logger.LogCritical("Killing process due to repeated feature flag refresh failures.");
                Writer.Info(LogGroup.FeatureFlags, "Killing process due to FF failures");
                Console.WriteLine("Killing process due to FF failures.");
                Process.GetCurrentProcess().Kill(true);
                return;
            }

            try
            {
                await Task.Delay(TimeSpan.FromSeconds(30), stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
        }
    }
}
