using Roblox.Services;

namespace Roblox.Website.HostedServices;

public sealed class MachineBanEnforcementHostedService(
    IServiceScopeFactory scopeFactory,
    MachineBanEnforcementSignal signal,
    ILogger<MachineBanEnforcementHostedService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var processedAny = false;
                do
                {
                    using var scope = scopeFactory.CreateScope();
                    var service = scope.ServiceProvider.GetRequiredService<MachineBanService>();
                    processedAny = await service.TryProcessNextAsync();
                } while (processedAny && !stoppingToken.IsCancellationRequested);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception exception)
            {
                logger.LogError(exception, "Machine-ban enforcement worker scan failed");
            }

            await signal.WaitAsync(stoppingToken);
        }
    }
}
