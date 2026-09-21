using System.Net.Http;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text.Json.Serialization;
using Roblox.Cache;
using Roblox.Services.Exceptions;

namespace Roblox.Services;

public class PurchaseAttestationService : ServiceBase, IService
{
    private const int TicketByteLen = 16;
    private static readonly TimeSpan TicketTtl = TimeSpan.FromMinutes(5);

    public sealed record IssuedTicket(string ticketId, long expiresAtMs);

    private static readonly HttpClient TurnstileHttp = new HttpClient
    {
        Timeout = TimeSpan.FromSeconds(5),
    };

    private sealed class TurnstileResponse
    {
        [JsonPropertyName("success")] public bool Success { get; set; }
        [JsonPropertyName("error-codes")] public string[]? ErrorCodes { get; set; }
        [JsonPropertyName("challenge_ts")] public string? ChallengeTs { get; set; }
        [JsonPropertyName("hostname")] public string? Hostname { get; set; }
    }

    public async Task EnforceTurnstileAsync(string? token, string? remoteIp)
    {
        var secret = Roblox.Configuration.InvisibleTurnstileSecretKey;
        if (string.IsNullOrEmpty(secret))
            throw new RobloxException(503, 0, "Could not verify purchase");
        if (string.IsNullOrWhiteSpace(token))
            throw new RobloxException(400, 0, "Could not verify purchase");

        var form = new List<KeyValuePair<string, string>>
        {
            new("secret", secret),
            new("response", token),
        };
        if (!string.IsNullOrEmpty(remoteIp))
            form.Add(new("remoteip", remoteIp));

        try
        {
            using var content = new FormUrlEncodedContent(form);
            using var resp = await TurnstileHttp.PostAsync(
                "https://challenges.cloudflare.com/turnstile/v0/siteverify", content);
            if (!resp.IsSuccessStatusCode)
                throw new RobloxException(400, 0, "Could not verify purchase");
            var parsed = await resp.Content.ReadFromJsonAsync<TurnstileResponse>();
            if (parsed == null || !parsed.Success)
                throw new RobloxException(400, 0, "Could not verify purchase");
        }
        catch (RobloxException)
        {
            throw;
        }
        catch
        {
            throw new RobloxException(400, 0, "Could not verify purchase");
        }
    }

    public async Task<IssuedTicket> Issue(long userId, long assetId)
    {
        var ticketBytes = new byte[TicketByteLen];
        RandomNumberGenerator.Fill(ticketBytes);
        var ticketId = Convert.ToHexString(ticketBytes).ToLowerInvariant();
        await redis.StringSetAsync(RedisKey(userId, ticketId), assetId.ToString(), TicketTtl);
        var expiresAt = DateTimeOffset.UtcNow.Add(TicketTtl).ToUnixTimeMilliseconds();
        return new IssuedTicket(ticketId, expiresAt);
    }

    public async Task ConsumeOrThrow(long userId, string? ticketId, long assetId)
    {
        if (string.IsNullOrWhiteSpace(ticketId) || ticketId.Length != TicketByteLen * 2)
            throw new RobloxException(400, 0, "Could not verify purchase");
        foreach (var c in ticketId)
        {
            if (!(c is >= '0' and <= '9' or >= 'a' and <= 'f'))
                throw new RobloxException(400, 0, "Could not verify purchase");
        }
        var raw = await redis.StringGetDeleteAsync(RedisKey(userId, ticketId));
        if (raw == null)
            throw new RobloxException(400, 0, "Could not verify purchase");
        if (!long.TryParse(raw, out var storedAssetId) || storedAssetId != assetId)
            throw new RobloxException(400, 0, "Could not verify purchase");
    }

    private static string RedisKey(long userId, string ticketId) =>
        $"econ:attest:v2:{userId}:{ticketId}";

    public bool IsThreadSafe() => true;
    public bool IsReusable() => false;
}
