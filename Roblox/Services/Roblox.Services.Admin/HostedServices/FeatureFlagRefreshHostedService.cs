using Roblox.Services.App.FeatureFlags;

namespace Roblox.Services.Admin.HostedServices;

public sealed class FeatureFlagRefreshHostedService : BackgroundService
{
    private readonly ILogger<FeatureFlagRefreshHostedService> _logger;

    public FeatureFlagRefreshHostedService(ILogger<FeatureFlagRefreshHostedService> logger)
    {
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(30));
        try
        {
            while (await timer.WaitForNextTickAsync(stoppingToken))
            {
                try
                {
                    await FeatureFlags.RefreshOnceAsync();
                }
                catch (Exception exception)
                {
                    _logger.LogError(exception, "Error updating feature flags.");
                }
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
        }
    }
}
