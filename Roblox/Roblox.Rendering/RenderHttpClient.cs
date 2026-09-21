using System.Net.Http.Json;

namespace Roblox.Rendering;

public static class RenderHttpClient
{
    private static readonly object Gate = new();
    private static HttpClient _client = CreateClient("http://127.0.0.1:3521/", string.Empty);
    private static bool _useBinaryTransport = true;

    public static void Configure(string baseUrl, string authorization, bool useBinaryTransport = true)
    {
        if (string.IsNullOrWhiteSpace(baseUrl)) throw new ArgumentException("Arbiter render URL is required", nameof(baseUrl));
        lock (Gate)
        {
            var old = _client;
            _client = CreateClient(baseUrl, authorization);
            _useBinaryTransport = useBinaryTransport;
            old.Dispose();
        }
    }

    public static void Configure(HttpClient client, bool useBinaryTransport = true)
    {
        ArgumentNullException.ThrowIfNull(client);
        if (client.BaseAddress == null) throw new ArgumentException("The render client must have a base address", nameof(client));
        lock (Gate) { var old = _client; _client = client; _useBinaryTransport = useBinaryTransport; if (!ReferenceEquals(old, client)) old.Dispose(); }
    }

    public static async Task<RenderResult> SendAsync(RenderRequest request, CancellationToken cancellationToken)
    {
        var output = await SendBytesAsync(request, cancellationToken);
        return new RenderResult
        {
            JobId = output.JobId,
            ContentType = output.ContentType,
            Data = Convert.ToBase64String(output.Data),
        };
    }

    public static async Task<RenderClientOutput> SendBytesAsync(RenderRequest request, CancellationToken cancellationToken)
    {
        HttpClient client;
        bool useBinary;
        lock (Gate) { client = _client; useBinary = _useBinaryTransport; }
        try { return await SendOnceAsync(client, request, useBinary, cancellationToken); }
        catch (HttpRequestException ex) when (useBinary && ex.StatusCode is System.Net.HttpStatusCode.NotFound or System.Net.HttpStatusCode.MethodNotAllowed)
        {
            return await SendOnceAsync(client, request, false, cancellationToken);
        }
    }

    private static async Task<RenderClientOutput> SendOnceAsync(HttpClient client, RenderRequest request, bool useBinary,
        CancellationToken cancellationToken)
    {
        using var message = new HttpRequestMessage(HttpMethod.Post, useBinary ? "render/v2" : "render")
        {
            Content = JsonContent.Create(request),
        };
        using var response = await client.SendAsync(message, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            var error = await response.Content.ReadFromJsonAsync<RenderErrorResponse>(cancellationToken: cancellationToken);
            throw new HttpRequestException(error?.Errors.FirstOrDefault()?.Message ?? $"Render failed with HTTP {(int)response.StatusCode}", null, response.StatusCode);
        }
        if (!useBinary)
        {
            var legacy = await response.Content.ReadFromJsonAsync<RenderResult>(cancellationToken: cancellationToken)
                         ?? throw new InvalidDataException("Arbiter returned an empty render response");
            return new RenderClientOutput(legacy.JobId, legacy.ContentType, Convert.FromBase64String(legacy.Data));
        }

        var data = await response.Content.ReadAsByteArrayAsync(cancellationToken);
        var contentType = response.Content.Headers.ContentType?.MediaType ?? "application/octet-stream";
        var jobId = response.Headers.TryGetValues("X-Render-Job-Id", out var values) &&
                    Guid.TryParse(values.FirstOrDefault(), out var parsed) ? parsed : Guid.Empty;
        var timings = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase);
        if (response.Headers.TryGetValues("Server-Timing", out var timingValues))
        {
            foreach (var item in timingValues.SelectMany(value => value.Split(',')))
            {
                var parts = item.Trim().Split(';', 2);
                if (parts.Length == 2 && parts[1].StartsWith("dur=", StringComparison.OrdinalIgnoreCase) &&
                    double.TryParse(parts[1].AsSpan(4), System.Globalization.NumberStyles.Float,
                        System.Globalization.CultureInfo.InvariantCulture, out var duration) && double.IsFinite(duration))
                    timings[parts[0]] = duration;
            }
        }
        return new RenderClientOutput(jobId, contentType, data, timings);
    }

    private static HttpClient CreateClient(string baseUrl, string authorization)
    {
        // The arbiter owns the RCC/startup deadline and maps it to 504. A competing
        // client timeout used to abandon a shared render at exactly the same moment,
        // leaving callers without the arbiter's stable error response.
        var client = new HttpClient { BaseAddress = new Uri(baseUrl.TrimEnd('/') + "/"), Timeout = Timeout.InfiniteTimeSpan };
        if (!string.IsNullOrWhiteSpace(authorization)) client.DefaultRequestHeaders.TryAddWithoutValidation("rblx-authorization", authorization);
        return client;
    }
}

public sealed record RenderClientOutput(Guid JobId, string ContentType, byte[] Data,
    IReadOnlyDictionary<string, double>? Timings = null);
