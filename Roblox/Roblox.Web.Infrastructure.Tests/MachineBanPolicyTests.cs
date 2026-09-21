using System.Net;
using System.Net.NetworkInformation;
using System.Reflection;
using Roblox.Libraries.DiscordApi;
using Roblox.Services;
using Roblox.Web.Infrastructure.Metadata;
using Roblox.Website.Controllers;

namespace Roblox.Web.Infrastructure.Tests;

public class MachineBanPolicyTests
{
    [Fact]
    public void ValidateMachine_RequiresSessionAndRobloxClientMetadata()
    {
        var method = typeof(BypassController).GetMethod(nameof(BypassController.ValidateMachine))!;

        Assert.NotNull(method.GetCustomAttribute<RequireRobloxSessionAttribute>());
        Assert.NotNull(method.GetCustomAttribute<RequireRobloxClientAttribute>());
    }

    [Theory]
    [InlineData("0A0027000000")]
    [InlineData("0A00270000FF")]
    [InlineData("001A7DDA7100")]
    [InlineData("001A7DDA71AB")]
    [InlineData("005056C00000")]
    [InlineData("005056C000EF")]
    [InlineData("02004C4F4F50")]
    public void GenericOrVirtualMacAddresses_AreExcludedFromMachineMatching(string address)
    {
        Assert.True(MachineBanService.IsExcludedMachineBanAddress(PhysicalAddress.Parse(address)));
    }

    [Theory]
    [InlineData("0A0027000100")]
    [InlineData("001A7DDA7200")]
    [InlineData("005056C00100")]
    [InlineData("02004C4F4F51")]
    [InlineData("AABBCCDDEEFF")]
    public void SpecificPhysicalMacAddresses_RemainMatchable(string address)
    {
        Assert.False(MachineBanService.IsExcludedMachineBanAddress(PhysicalAddress.Parse(address)));
    }

    [Fact]
    public void DiscordBanGate_RequiresExactInternalAndPublicReasonMatch()
    {
        const string reason = "Public reason";

        Assert.True(PermanentAccountTerminationService.ShouldBanDiscord(reason, reason));
        Assert.False(PermanentAccountTerminationService.ShouldBanDiscord(reason, null));
        Assert.False(PermanentAccountTerminationService.ShouldBanDiscord(reason, "public reason"));
        Assert.False(PermanentAccountTerminationService.ShouldBanDiscord(reason, reason + " "));
    }

    [Fact]
    public void AltAccountScore_ExactMacSetAlwaysOutranksPartialAndIpCannotCreateCandidate()
    {
        var exactWithoutIp = Roblox.Services.AdminApi.AdminApiService.CalculateAltAccountScore(true, 1, 0);
        var strongestPartialWithIp = Roblox.Services.AdminApi.AdminApiService.CalculateAltAccountScore(false, 0.999, 50);

        Assert.Equal(90, exactWithoutIp);
        Assert.True(exactWithoutIp > strongestPartialWithIp);
        Assert.Equal(0, Roblox.Services.AdminApi.AdminApiService.CalculateAltAccountScore(false, 0, 50));
        Assert.Equal(100, Roblox.Services.AdminApi.AdminApiService.CalculateAltAccountScore(true, 1, 2));
    }

    [Fact]
    public async Task DiscordGuildBan_UsesPermanentBanEndpointWithoutDeletingMessages()
    {
        var handler = new RecordingHandler();
        using var client = new HttpClient(handler) { BaseAddress = new Uri("https://discord.test/api/") };
        var api = new DiscordBotApi(client, "token");

        var result = await api.BanGuildMember("guild-1", "discord-2", "Averia user 123");

        Assert.True(result);
        Assert.NotNull(handler.Request);
        Assert.Equal(HttpMethod.Put, handler.Request!.Method);
        Assert.Equal("https://discord.test/api/guilds/guild-1/bans/discord-2", handler.Request.RequestUri!.ToString());
        Assert.Equal("Averia%20user%20123", handler.Request.Headers.GetValues("X-Audit-Log-Reason").Single());
        Assert.Equal("{\"delete_message_seconds\":0}", handler.Body);
    }

    private sealed class RecordingHandler : HttpMessageHandler
    {
        public HttpRequestMessage? Request { get; private set; }
        public string? Body { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            Request = request;
            Body = request.Content == null ? null : await request.Content.ReadAsStringAsync(cancellationToken);
            return new HttpResponseMessage(HttpStatusCode.NoContent);
        }
    }
}
