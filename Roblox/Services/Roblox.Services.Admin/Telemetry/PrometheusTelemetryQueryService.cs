using System.Globalization;
using System.Text.Json;
using Microsoft.Extensions.Caching.Memory;

namespace Roblox.Services.Admin.Telemetry;

public sealed class PrometheusTelemetryQueryService : ITelemetryQueryService
{
    private static readonly IReadOnlyDictionary<string, RangeDefinition> Ranges =
        new Dictionary<string, RangeDefinition>(StringComparer.OrdinalIgnoreCase)
        {
            ["1h"] = new(TimeSpan.FromHours(1), 15),
            ["6h"] = new(TimeSpan.FromHours(6), 60),
            ["24h"] = new(TimeSpan.FromHours(24), 300),
            ["7d"] = new(TimeSpan.FromDays(7), 1800),
            ["30d"] = new(TimeSpan.FromDays(30), 7200),
        };

    private static readonly QueryDefinition[] Queries =
    {
        new("requests", "Request rate", "requests/s", "sum(rate(http_server_request_duration_seconds_count{FILTER}[5m]))"),
        new("errors", "Server error rate", "%", "100 * sum(rate(http_server_request_duration_seconds_count{FILTER,STATUS}[5m])) / clamp_min(sum(rate(http_server_request_duration_seconds_count{FILTER}[5m])), 0.000001)"),
        new("request_p95", "Request latency p95", "ms", "1000 * histogram_quantile(0.95, sum(rate(http_server_request_duration_seconds_bucket{FILTER}[5m])) by (le))"),
        new("database_p95", "Database latency p95", "ms", "histogram_quantile(0.95, sum(rate(roblox_database_operation_duration_milliseconds_bucket{FILTER}[5m])) by (le))"),
        new("cache_hits", "Cache hit rate", "%", "100 * sum(rate(roblox_cache_lookups_total{FILTER,CACHEHIT}[5m])) / clamp_min(sum(rate(roblox_cache_lookups_total{FILTER}[5m])), 0.000001)"),
        new("signups", "Signups", "signups", "sum(increase(roblox_user_events_total{FILTER,USEREVENT}[5m]))"),
        new("robux", "Robux volume", "Robux", "sum(increase(roblox_economy_robux_volume_robux_total{FILTER}[5m]))"),
        new("purchases", "Purchase latency p95", "ms", "histogram_quantile(0.95, sum(rate(roblox_economy_purchase_duration_milliseconds_bucket{FILTER}[5m])) by (le))"),
        new("renders", "Render latency p95", "ms", "histogram_quantile(0.95, sum(rate(roblox_render_duration_milliseconds_bucket{FILTER}[5m])) by (le))"),
        new("game_joins", "Game join events", "events", "sum(increase(roblox_game_join_events_total{FILTER}[5m]))"),
        new("flood_checks", "Flood-check hits", "hits", "sum(increase(roblox_flood_check_hits_total{FILTER}[5m]))"),
        new("security", "Security events", "events", "sum(increase(roblox_security_events_total{FILTER}[5m]))"),
    };

    private readonly HttpClient _client;
    private readonly IMemoryCache _cache;
    private readonly TimeProvider _timeProvider;
    private readonly IRenderStatisticsClient? _renderStatistics;

    public PrometheusTelemetryQueryService(HttpClient client, IMemoryCache cache, TimeProvider timeProvider,
        IRenderStatisticsClient? renderStatistics = null)
    {
        _client = client;
        _cache = cache;
        _timeProvider = timeProvider;
        _renderStatistics = renderStatistics;
    }

    public async Task<TelemetryDashboardResponse> GetDashboardAsync(string range, string service, CancellationToken cancellationToken)
    {
        if (!Ranges.TryGetValue(range, out var rangeDefinition))
            throw new ArgumentException("Unsupported telemetry range.", nameof(range));

        service = string.IsNullOrWhiteSpace(service) ? "all" : service.Trim();
        var cacheKey = $"telemetry:{range.ToLowerInvariant()}:{service}";
        if (_cache.TryGetValue(cacheKey, out TelemetryDashboardResponse? cached) && cached != null)
            return cached;

        var services = await GetServicesAsync(cancellationToken);
        if (!service.Equals("all", StringComparison.OrdinalIgnoreCase) && !services.Contains(service, StringComparer.Ordinal))
            throw new ArgumentException("Unknown telemetry service.", nameof(service));

        var now = _timeProvider.GetUtcNow();
        var start = now - rangeDefinition.Duration;
        var queryTasks = Queries.Select(query => QueryRangeAsync(
            query,
            BuildExpression(query.Expression, service),
            start,
            now,
            rangeDefinition.StepSeconds,
            cancellationToken)).ToArray();
        var charts = await Task.WhenAll(queryTasks);

        double? Last(string key) => charts.First(chart => chart.Key == key).Series
            .SelectMany(series => series.Points)
            .OrderBy(point => point.Timestamp)
            .LastOrDefault()?.Value;

        var renderPool = _renderStatistics == null ? null : await _renderStatistics.GetAsync(cancellationToken);
        var response = new TelemetryDashboardResponse(
            now.UtcDateTime,
            range.ToLowerInvariant(),
            rangeDefinition.StepSeconds,
            service,
            services,
            new TelemetrySummary(Last("requests"), Last("errors"), Last("request_p95"), Last("database_p95"), Last("cache_hits"), Last("signups"), Last("robux")),
            charts,
            renderPool);
        _cache.Set(cacheKey, response, TimeSpan.FromSeconds(15));
        return response;
    }

    private async Task<IReadOnlyList<string>> GetServicesAsync(CancellationToken cancellationToken)
    {
        if (_cache.TryGetValue("telemetry:services", out IReadOnlyList<string>? cached) && cached != null)
            return cached;

        using var document = await GetPrometheusDocumentAsync("api/v1/label/service_name/values", cancellationToken);
        var services = document.RootElement.GetProperty("data").EnumerateArray()
            .Select(value => value.GetString())
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Select(value => value!)
            .Distinct(StringComparer.Ordinal)
            .OrderBy(value => value, StringComparer.Ordinal)
            .ToArray();
        _cache.Set("telemetry:services", services, TimeSpan.FromMinutes(1));
        return services;
    }

    private async Task<TelemetryChart> QueryRangeAsync(QueryDefinition definition, string expression, DateTimeOffset start,
        DateTimeOffset end, int stepSeconds, CancellationToken cancellationToken)
    {
        var uri = $"api/v1/query_range?query={Uri.EscapeDataString(expression)}&start={start.ToUnixTimeSeconds()}&end={end.ToUnixTimeSeconds()}&step={stepSeconds}";
        using var document = await GetPrometheusDocumentAsync(uri, cancellationToken);
        var result = document.RootElement.GetProperty("data").GetProperty("result");
        var series = new List<TelemetrySeries>();
        foreach (var item in result.EnumerateArray())
        {
            var metric = item.GetProperty("metric");
            var name = metric.TryGetProperty("service_name", out var serviceName) ? serviceName.GetString() ?? "total" : "total";
            var points = new List<TelemetryPoint>();
            foreach (var value in item.GetProperty("values").EnumerateArray())
            {
                if (!value[0].TryGetDouble(out var timestamp)) continue;
                var text = value[1].GetString();
                if (!double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var number) || !double.IsFinite(number)) continue;
                points.Add(new TelemetryPoint(DateTimeOffset.FromUnixTimeMilliseconds((long)(timestamp * 1000)).UtcDateTime, number));
            }
            series.Add(new TelemetrySeries(name, points));
        }
        return new TelemetryChart(definition.Key, definition.Title, definition.Unit, series);
    }

    private async Task<JsonDocument> GetPrometheusDocumentAsync(string uri, CancellationToken cancellationToken)
    {
        try
        {
            using var response = await _client.GetAsync(uri, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
            if (!response.IsSuccessStatusCode)
                throw new TelemetryQueryException($"Prometheus returned HTTP {(int)response.StatusCode}.");
            var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
            var document = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);
            if (!document.RootElement.TryGetProperty("status", out var status) || status.GetString() != "success")
            {
                document.Dispose();
                throw new TelemetryQueryException("Prometheus returned an unsuccessful response.");
            }
            return document;
        }
        catch (TelemetryQueryException) { throw; }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            throw new TelemetryQueryException("Prometheus query timed out.");
        }
        catch (Exception exception) when (exception is HttpRequestException or JsonException)
        {
            throw new TelemetryQueryException("Prometheus is unavailable or returned invalid data.", exception);
        }
    }

    private static string BuildExpression(string expression, string service)
    {
        var serviceFilter = service.Equals("all", StringComparison.OrdinalIgnoreCase)
            ? string.Empty
            : $"service_name=\"{service.Replace("\\", "\\\\").Replace("\"", "\\\"")}\"";
        return expression
            .Replace("FILTER", serviceFilter, StringComparison.Ordinal)
            .Replace("STATUS", "http_response_status_code=~\"5..\"", StringComparison.Ordinal)
            .Replace("CACHEHIT", "cache_result=\"hit\"", StringComparison.Ordinal)
            .Replace("USEREVENT", "user_event=\"signup\"", StringComparison.Ordinal)
            .Replace("{,", "{", StringComparison.Ordinal)
            .Replace("{FILTER}", "{}", StringComparison.Ordinal);
    }
    private sealed record RangeDefinition(TimeSpan Duration, int StepSeconds);
    private sealed record QueryDefinition(string Key, string Title, string Unit, string Expression);
}
