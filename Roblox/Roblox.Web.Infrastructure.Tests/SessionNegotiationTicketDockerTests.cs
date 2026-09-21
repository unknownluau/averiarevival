using Roblox.Services;

namespace Roblox.Web.Infrastructure.Tests;

public class SessionNegotiationTicketDockerTests
{
    [Fact]
    public async Task Ticket_IsOpaqueAndCanOnlyBeConsumedOnce()
    {
        if (await DockerInfrastructureFixture.CreateAsync() == null)
        {
            return;
        }

        using var service = new SessionNegotiationTicketService();
        const string sessionToken = "signed-session-token";
        const string ip = "ip";

        var ticket = await service.IssueAsync(sessionToken, ip);

        Assert.Equal(64, ticket.Length);
        Assert.DoesNotContain(sessionToken, ticket, StringComparison.Ordinal);
        Assert.Equal(sessionToken, await service.ConsumeAsync(ticket, ip));
        Assert.Null(await service.ConsumeAsync(ticket, ip));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("too-short")]
    [InlineData("zzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzz")]
    public async Task InvalidTicket_IsRejectedWithoutRedisLookup(string? ticket)
    {
        if (await DockerInfrastructureFixture.CreateAsync() == null)
        {
            return;
        }

        using var service = new SessionNegotiationTicketService();
        Assert.Null(await service.ConsumeAsync(ticket, "ip"));
    }
}
