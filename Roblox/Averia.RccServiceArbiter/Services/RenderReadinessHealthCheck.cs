using System.Net.Sockets;
using Averia.RccServiceArbiter.Configuration;
using Averia.RccServiceArbiter.Rendering;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;

namespace Averia.RccServiceArbiter.Services;

public sealed class RenderReadinessHealthCheck(IRenderService renderer, IOptions<ArbiterOptions> options) : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        var configuration = options.Value;
        var executable = Path.Combine(configuration.RccServiceRoot,
            $"RCCService{configuration.Render.DefaultYear}", "RCCService.exe");
        if (!File.Exists(executable))
            return HealthCheckResult.Unhealthy("The configured RCCService executable does not exist");
        if (!renderer.IsReady)
            return HealthCheckResult.Unhealthy("The minimum RCC render pool has not finished warming");

        var originText = string.IsNullOrWhiteSpace(configuration.Render.OriginBaseUrl)
            ? configuration.BaseUrl
            : configuration.Render.OriginBaseUrl;
        if (!Uri.TryCreate(originText, UriKind.Absolute, out var origin) || string.IsNullOrWhiteSpace(origin.Host))
            return HealthCheckResult.Unhealthy("The render origin URL is invalid");

        try
        {
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeout.CancelAfter(TimeSpan.FromSeconds(2));
            using var tcp = new TcpClient();
            await tcp.ConnectAsync(origin.Host, origin.Port, timeout.Token);
            return HealthCheckResult.Healthy();
        }
        catch (Exception ex) when (ex is SocketException or OperationCanceledException)
        {
            return HealthCheckResult.Unhealthy("The private render origin is unreachable", ex);
        }
    }
}
