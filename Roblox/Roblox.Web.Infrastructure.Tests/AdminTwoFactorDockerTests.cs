using Roblox.Web.Infrastructure.Admin;

namespace Roblox.Web.Infrastructure.Tests;

public class AdminTwoFactorDockerTests
{
    [Fact]
    public async Task AdminTwoFactorStore_RoundTripsVerificationStateInRedis()
    {
        var fixture = await DockerInfrastructureFixture.CreateAsync();
        if (fixture == null)
        {
            return;
        }

        var store = new AdminTwoFactorStore();
        var userId = Random.Shared.NextInt64(1, long.MaxValue);
        var sessionId = Guid.NewGuid().ToString("N");

        await store.InvalidateAsync(userId, sessionId);
        Assert.False(await store.IsVerifiedAsync(userId, sessionId));

        await store.MarkVerifiedAsync(userId, sessionId);
        Assert.True(await store.IsVerifiedAsync(userId, sessionId));

        await store.InvalidateAsync(userId, sessionId);
        Assert.False(await store.IsVerifiedAsync(userId, sessionId));
    }
}
