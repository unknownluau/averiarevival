using Roblox.Web.Infrastructure.Auth;

namespace Roblox.Web.Infrastructure.Tests;

public class AuthSessionTokenCodecTests
{
    [Fact]
    public void Configure_RejectsEmptyKey()
    {
        Assert.Throws<ArgumentException>(() => RobloxSessionTokenCodec.Configure(""));
    }

    [Fact]
    public void Configure_IsIdempotentForSameKey()
    {
        InfrastructureTestHelpers.TryConfigureSessionJwt();

        RobloxSessionTokenCodec.Configure(TestConstants.SessionJwtKey);
    }

    [Fact]
    public void SignedPayload_RoundTrips()
    {
        InfrastructureTestHelpers.TryConfigureSessionJwt();
        var payload = new SessionTokenPayload
        {
            sessionId = "session-123",
            createdAt = 12345,
        };

        var token = RobloxSessionTokenCodec.CreateJwt(payload);
        var decoded = RobloxSessionTokenCodec.DecodeJwt<SessionTokenPayload>(token);

        Assert.Equal(payload.sessionId, decoded.sessionId);
        Assert.Equal(payload.createdAt, decoded.createdAt);
    }

    [Fact]
    public void TamperedPayload_IsRejected()
    {
        InfrastructureTestHelpers.TryConfigureSessionJwt();
        var token = RobloxSessionTokenCodec.CreateJwt(new SessionTokenPayload
        {
            sessionId = "session-123",
            createdAt = 12345,
        });

        Assert.ThrowsAny<Exception>(() => RobloxSessionTokenCodec.DecodeJwt<SessionTokenPayload>(token + "tampered"));
    }
}
