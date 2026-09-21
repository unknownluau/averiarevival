using System.Net.Http.Json;

namespace Roblox.Services.Admin.Telemetry;

public sealed class ArbiterRenderStatisticsClient(HttpClient client, ILogger<ArbiterRenderStatisticsClient> logger)
    : IRenderStatisticsClient
{
    public async Task<RenderPoolSnapshot?> GetAsync(CancellationToken cancellationToken)
    {
        try
        {
            using var response = await client.GetAsync("render/statistics", HttpCompletionOption.ResponseHeadersRead,
                cancellationToken);
            if (!response.IsSuccessStatusCode) return null;
            return await response.Content.ReadFromJsonAsync<RenderPoolSnapshot>(cancellationToken: cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { throw; }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            logger.LogWarning(ex, "RCC Arbiter render statistics are unavailable");
            return null;
        }
    }
}
