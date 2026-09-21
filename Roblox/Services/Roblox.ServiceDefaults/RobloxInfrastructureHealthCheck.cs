using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Roblox.ServiceDefaults;

public sealed class RobloxInfrastructureHealthCheck : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            await using var connection = Roblox.Services.Database.connection;
            await connection.OpenAsync(cancellationToken);
            await connection.CloseAsync();

            if (!Roblox.Cache.DistributedCache.redis.IsConnected)
            {
                return HealthCheckResult.Unhealthy("Redis is not connected.");
            }

            return HealthCheckResult.Healthy();
        }
        catch (Exception ex)
        {
            return HealthCheckResult.Unhealthy("Infrastructure dependency check failed.", ex);
        }
    }
}
