using System.Net;
using System.Text;
using Microsoft.Extensions.Caching.Memory;
using Roblox.Services.Admin.Telemetry;

namespace Roblox.Services.Admin.Tests;

public sealed class PrometheusTelemetryQueryServiceTests
{
    [Fact]
    public async Task Dashboard_ParsesRangeDataAndSkipsNonFiniteValues()
    {
        var handler = new PrometheusHandler();
        var service = CreateService(handler);

        var result = await service.GetDashboardAsync("1h", "Roblox.Website", default);

        Assert.Equal(15, result.StepSeconds);
        Assert.Contains("Roblox.Website", result.AvailableServices);
        Assert.All(result.Charts, chart => Assert.Single(chart.Series));
        Assert.All(result.Charts, chart => Assert.Single(chart.Series[0].Points));
        Assert.DoesNotContain(handler.RequestUris, uri => uri.Contains("userId", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task Dashboard_RejectsUnknownServiceBeforeExecutingQueries()
    {
        var handler = new PrometheusHandler();
        var service = CreateService(handler);

        await Assert.ThrowsAsync<ArgumentException>(() => service.GetDashboardAsync("6h", "unknown-service", default));

        Assert.Single(handler.RequestUris);
    }

    [Fact]
    public async Task Dashboard_RejectsUnsupportedRange()
    {
        var service = CreateService(new PrometheusHandler());
        await Assert.ThrowsAsync<ArgumentException>(() => service.GetDashboardAsync("2y", "all", default));
    }

    [Fact]
    public async Task Dashboard_MapsInvalidUpstreamDataToTelemetryException()
    {
        var service = CreateService(new PrometheusHandler(invalidJson: true));
        await Assert.ThrowsAsync<TelemetryQueryException>(() => service.GetDashboardAsync("6h", "all", default));
    }

    private static PrometheusTelemetryQueryService CreateService(HttpMessageHandler handler)
    {
        var client = new HttpClient(handler) { BaseAddress = new Uri("http://prometheus/"), Timeout = TimeSpan.FromSeconds(5) };
        return new PrometheusTelemetryQueryService(client, new MemoryCache(new MemoryCacheOptions()), TimeProvider.System);
    }

    private sealed class PrometheusHandler : HttpMessageHandler
    {
        private readonly bool _invalidJson;
        public List<string> RequestUris { get; } = new();

        public PrometheusHandler(bool invalidJson = false) => _invalidJson = invalidJson;

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            lock (RequestUris) RequestUris.Add(request.RequestUri!.ToString());
            var body = _invalidJson
                ? "not-json"
                : request.RequestUri!.AbsolutePath.Contains("label/service_name", StringComparison.Ordinal)
                    ? "{\"status\":\"success\",\"data\":[\"Roblox.Website\",\"Roblox.Services.Api\"]}"
                    : "{\"status\":\"success\",\"data\":{\"resultType\":\"matrix\",\"result\":[{\"metric\":{\"service_name\":\"Roblox.Website\"},\"values\":[[1700000000,\"12.5\"],[1700000015,\"NaN\"]]}]}}";
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(body, Encoding.UTF8, "application/json"),
            });
        }
    }
}
