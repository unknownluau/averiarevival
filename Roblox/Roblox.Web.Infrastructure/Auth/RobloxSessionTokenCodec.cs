using System.Text.Json;
using JWT;
using JWT.Algorithms;
using JWT.Serializers;

namespace Roblox.Web.Infrastructure.Auth;

public static class RobloxSessionTokenCodec
{
    private static readonly IJwtAlgorithm Algorithm = new HMACSHA512Algorithm();
    private static readonly IJsonSerializer Serializer = new JsonNetSerializer();
    private static readonly IBase64UrlEncoder UrlEncoder = new JwtBase64UrlEncoder();
    private static readonly IDateTimeProvider DateTimeProvider = new UtcDateTimeProvider();
    private static readonly IJwtValidator Validator = new JwtValidator(Serializer, DateTimeProvider);
    private static readonly IJwtEncoder Encoder = new JwtEncoder(Algorithm, Serializer, UrlEncoder);
    private static readonly IJwtDecoder Decoder = new JwtDecoder(Serializer, Validator, UrlEncoder, Algorithm);

    private static string? _cookieJwtKey;

    public static void Configure(string newJwtKey)
    {
        if (string.IsNullOrWhiteSpace(newJwtKey))
        {
            throw new ArgumentException("JWT key cannot be empty.", nameof(newJwtKey));
        }

        if (!string.IsNullOrEmpty(_cookieJwtKey))
        {
            if (_cookieJwtKey == newJwtKey)
            {
                return;
            }

            throw new InvalidOperationException("Session JWT codec already configured.");
        }

        _cookieJwtKey = newJwtKey;
    }

    public static string CreateJwt<T>(T obj)
    {
        EnsureConfigured();
        return Encoder.Encode(obj, _cookieJwtKey!);
    }

    public static T DecodeJwt<T>(string token)
    {
        EnsureConfigured();
        var json = Decoder.Decode(token, _cookieJwtKey!, verify: true);
        var result = JsonSerializer.Deserialize<T>(json);
        return result ?? throw new NullReferenceException();
    }

    private static void EnsureConfigured()
    {
        if (string.IsNullOrWhiteSpace(_cookieJwtKey))
        {
            throw new InvalidOperationException("Session JWT codec has not been configured.");
        }
    }
}
