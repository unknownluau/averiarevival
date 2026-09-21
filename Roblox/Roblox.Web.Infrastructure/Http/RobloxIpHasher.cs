using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Http;

namespace Roblox.Web.Infrastructure.Http;

public static class RobloxIpHasher
{
    private const string IpHashSetupRedisKey = "IpHashKeyV1";
    private static readonly SemaphoreSlim SetupLock = new(1, 1);
    private static IpHashSetupSnapshot? setupSnapshot;

    private sealed class RedisIpHashSetupV1
    {
        public Dictionary<string, string> digitToGuid { get; init; } = new();
        public string endKey { get; init; } = string.Empty;
    }

    private sealed class IpHashSetupSnapshot
    {
        public required string[] DigitToGuid { get; init; }
        public required string EndKey { get; init; }
    }

    public static string GetRequesterIpRaw(HttpContext ctx)
    {
        if (ctx.Request.Headers.TryGetValue("cf-connecting-ip", out var cloudflareIp) &&
            !string.IsNullOrWhiteSpace(cloudflareIp.ToString()))
        {
            return cloudflareIp.ToString();
        }

        var ipString = ctx.Connection.RemoteIpAddress?.ToString();
        if (string.IsNullOrWhiteSpace(ipString))
        {
            throw new Exception("Bad IP address - empty or null string");
        }

        return ipString;
    }

    public static async Task InitializeIpHashSetupAsync()
    {
        if (setupSnapshot != null)
        {
            return;
        }

        await SetupLock.WaitAsync();
        try
        {
            if (setupSnapshot != null)
            {
                return;
            }

            var data = await Roblox.Services.Cache.distributed.StringGetAsync(IpHashSetupRedisKey);
            if (string.IsNullOrWhiteSpace(data))
            {
                var created = CreateRedisSetup();
                await Roblox.Services.Cache.distributed.StringSetAsync(IpHashSetupRedisKey, JsonSerializer.Serialize(created));
                setupSnapshot = CreateSnapshot(created);
                return;
            }

            var setup = JsonSerializer.Deserialize<RedisIpHashSetupV1>(data)
                ?? throw new Exception("Bad IP hash setup - Redis payload could not be parsed");
            setupSnapshot = CreateSnapshot(setup);
        }
        finally
        {
            SetupLock.Release();
        }
    }

    public static ulong ConvertFromIpAddressToInteger(string ipAddress)
    {
        var address = IPAddress.Parse(ipAddress);
        address = address.MapToIPv6();
        var bytes = address.GetAddressBytes();

        if (BitConverter.IsLittleEndian)
        {
            Array.Reverse(bytes);
        }

        return BitConverter.ToUInt64(bytes, 0);
    }

    public static string GetIp(string trueIp, string? salt = null)
    {
        var setup = setupSnapshot ?? throw new InvalidOperationException(
            "IP hash setup is not initialized. Call RobloxIpHasher.InitializeIpHashSetupAsync() during app startup.");
        var ip = ConvertFromIpAddressToInteger(trueIp);
        var ipString = ip.ToString();
        var first = ipString[0] - '0';
        var last = ipString[^1] - '0';
        var keyToUse = setup.DigitToGuid[first];
        if (ipString[0] is '2' or '6' or '3' or '7')
        {
            keyToUse = new string(keyToUse.ToCharArray().Reverse().ToArray());
        }

        var key = new StringBuilder(keyToUse);
        key.Append(ip);
        key.Append(last != 9 ? setup.DigitToGuid[last] : setup.DigitToGuid[last].ToUpperInvariant());

        for (var i = 0; i < ipString.Length; i++)
        {
            var toAdd = setup.DigitToGuid[ipString[i] - '0'];
            key.Append(toAdd.Length >= i + 1 ? toAdd.AsSpan(i, 1) : toAdd.AsSpan(0, Math.Min(2, toAdd.Length)));
        }

        if (salt != null)
        {
            key.Append(salt);
        }

        using var alg = SHA512.Create();
        key.Append(setup.EndKey);
        var hash = alg.ComputeHash(Encoding.UTF8.GetBytes(key.ToString()));
        return Convert.ToBase64String(hash);
    }

    private static RedisIpHashSetupV1 CreateRedisSetup()
    {
        var digitToGuid = new Dictionary<string, string>();
        for (var i = 0; i < 10; i++)
        {
            digitToGuid[i.ToString()] = Guid.NewGuid() + Guid.NewGuid().ToString();
        }

        return new RedisIpHashSetupV1
        {
            digitToGuid = digitToGuid,
            endKey = Guid.NewGuid() + Guid.NewGuid().ToString(),
        };
    }

    private static IpHashSetupSnapshot CreateSnapshot(RedisIpHashSetupV1 setup)
    {
        var digitToGuid = new string[10];
        for (var i = 0; i < digitToGuid.Length; i++)
        {
            var key = i.ToString();
            if (!setup.digitToGuid.TryGetValue(key, out var value) || string.IsNullOrWhiteSpace(value))
            {
                throw new Exception($"Bad IP hash setup - missing digit key {key}");
            }

            digitToGuid[i] = value;
        }

        if (string.IsNullOrWhiteSpace(setup.endKey))
        {
            throw new Exception("Bad IP hash setup - missing end key");
        }

        return new IpHashSetupSnapshot
        {
            DigitToGuid = digitToGuid,
            EndKey = setup.endKey,
        };
    }

    internal static void SetIpHashSetupForTests(IReadOnlyDictionary<string, string> digitToGuid, string endKey)
    {
        setupSnapshot = CreateSnapshot(new RedisIpHashSetupV1
        {
            digitToGuid = new Dictionary<string, string>(digitToGuid),
            endKey = endKey,
        });
    }

    internal static void ResetIpHashSetupForTests()
    {
        setupSnapshot = null;
    }
}
