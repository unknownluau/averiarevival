using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace Roblox.Libraries.LeakCheckApi;
public class LeakCheckApi
{
    private readonly HttpClient _httpClient;
    private readonly string _apiKey;
    private readonly string _baseUrl;

    public LeakCheckApi(string? apiKey = null, string baseUrl = "https://leakcheck.io/api/v2")
    {
        _apiKey = Environment.GetEnvironmentVariable("LEAKCHECK_APIKEY") ?? apiKey ?? string.Empty;
        var proxy = Environment.GetEnvironmentVariable("LEAKCHECK_PROXY");
        
        if (string.IsNullOrEmpty(_apiKey) || _apiKey.Length < 40)
            throw new ArgumentException("API key is missing, empty, or invalid (must be at least 40 characters long).");
        
        _baseUrl = baseUrl;
        
        var handler = new HttpClientHandler();
        
        if (!string.IsNullOrEmpty(proxy))
        {
            handler.Proxy = new WebProxy(proxy);
            handler.UseProxy = true;
        }
        
        _httpClient = new HttpClient(handler);
        _httpClient.DefaultRequestHeaders.Add("X-API-Key", _apiKey);
        _httpClient.DefaultRequestHeaders.Add("User-Agent",
            $"AveriaAPI/1.0");
    }

    public void SetProxy(string proxy)
    {
        if (string.IsNullOrEmpty(proxy))
            throw new ArgumentException("Proxy URL cannot be null or empty.");

        var handler = new HttpClientHandler
        {
            Proxy = new WebProxy(proxy),
            UseProxy = true
        };
    }

    public async Task<LeakCheckResponse> LookupAsync(string query, string? queryType = null, int limit = 100, int offset = 0)
    {
        if (limit > 1000)
            throw new ArgumentException("Limit cannot be greater than 1000.");
        if (offset > 2500)
            throw new ArgumentException("Offset cannot be greater than 2500.");

        string endpoint = $"{_baseUrl}/query/{Uri.EscapeDataString(query)}";
        string url = $"{endpoint}?limit={limit}&offset={offset}";
        if (!string.IsNullOrEmpty(queryType))
            url += $"&type={Uri.EscapeDataString(queryType)}";

        HttpResponseMessage response;
        try
        {
            response = await _httpClient.GetAsync(url);
        }
        catch (HttpRequestException e)
        {
            throw new InvalidOperationException($"Request failed: {e.Message}", e);
        }

        string jsonString = await response.Content.ReadAsStringAsync();

        if (!response.IsSuccessStatusCode)
            throw new InvalidOperationException($"API responded with an HTTP error ({response.StatusCode}).");

        var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
        var data = JsonSerializer.Deserialize<LeakCheckResponse>(jsonString, options);

        if (data == null || !data.Success)
            throw new InvalidOperationException("API responded with an error or invalid JSON.");

        return data;
    }

    public async Task<bool> IsPasswordLeaked(string password)
    {
        try
        {
            string phash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(password)))[..24].ToLowerInvariant();
            var result = await LookupAsync(phash, "phash");
            if (result.Found >= 1)
                return true;
        }
        catch (Exception) {}

        return false;
    }
    public void Dispose()
    {
        _httpClient?.Dispose();
    }
    public class LeakCheckResponse
    {
        [JsonPropertyName("success")]
        public bool Success { get; set; }

        [JsonPropertyName("quota")]
        public int Quota { get; set; }

        [JsonPropertyName("found")]
        public int Found { get; set; }

        [JsonPropertyName("result")]
        public List<LeakCheckResult>? Result { get; set; }
    }

    public class LeakCheckResult
    {
        [JsonPropertyName("username")]
        public string? Username { get; set; }
        [JsonPropertyName("password")]
        public string? Password { get; set; }

        [JsonPropertyName("source")]
        public LeakSource? Source { get; set; }

        [JsonPropertyName("origin")]
        public List<string>? Origin { get; set; }

        [JsonPropertyName("email")]
        public string? Email { get; set; }

        [JsonPropertyName("fields")]
        public List<string>? Fields { get; set; }
    }

    public class LeakSource
    {
        [JsonPropertyName("name")]
        public string? Name { get; set; }

        [JsonPropertyName("breach_date")]
        public string? BreachDate { get; set; }

        [JsonPropertyName("unverified")]
        public int? Unverified { get; set; }

        [JsonPropertyName("passwordless")]
        public int? Passwordless { get; set; }

        [JsonPropertyName("compilation")]
        public int? Compilation { get; set; }
    }
}
