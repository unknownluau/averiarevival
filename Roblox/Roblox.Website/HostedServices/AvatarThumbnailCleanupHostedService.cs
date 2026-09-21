using Roblox.Services;

namespace Roblox.Website.HostedServices;

public class AvatarThumbnailCleanupHostedService : BackgroundService
{
    private readonly ILogger<AvatarThumbnailCleanupHostedService> _logger;

    public AvatarThumbnailCleanupHostedService(ILogger<AvatarThumbnailCleanupHostedService> logger)
    {
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            await Task.Delay(AvatarService.CleanupStartupDelay, stoppingToken);
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            return;
        }

        using var timer = new PeriodicTimer(AvatarService.CleanupInterval);
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await AvatarService.ClearStale3DThumbnailsAsync();
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception e)
            {
                _logger.LogError(e, "Avatar 3D thumbnail cleanup failed.");
            }

            try
            {
                if (!await timer.WaitForNextTickAsync(stoppingToken))
                {
                    break;
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
        }
    }
}
