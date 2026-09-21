using System.Net;
using Microsoft.AspNetCore.Http;
using Roblox.Web.Infrastructure.Http;

namespace Roblox.Web.Infrastructure.Tests;

public class RobloxIpHasherTests
{
    [Fact]
    public void GetRequesterIpRaw_PrefersCloudflareConnectingIpHeader()
    {
        var context = new DefaultHttpContext();
        context.Connection.RemoteIpAddress = IPAddress.Parse("127.0.0.1");
        context.Request.Headers["cf-connecting-ip"] = "203.0.113.10";

        var result = RobloxIpHasher.GetRequesterIpRaw(context);

        Assert.Equal("203.0.113.10", result);
    }

    [Theory]
    [InlineData("127.0.0.1", 281472812449793UL)]
    [InlineData("::1", 1UL)]
    public void ConvertFromIpAddressToInteger_MapsIpToStableInteger(string ipAddress, ulong expected)
    {
        var result = RobloxIpHasher.ConvertFromIpAddressToInteger(ipAddress);

        Assert.Equal(expected, result);
    }

    [Fact]
    public void GetIp_WithKnownSetup_ReturnsStableHash()
    {
        RobloxIpHasher.SetIpHashSetupForTests(CreateKnownDigitMap(), "end-key");

        var result = RobloxIpHasher.GetIp("127.0.0.1");

        Assert.Equal(
            "NggFoyGRJLNPGCfO9PDA8blWysmfj/S5i41FdHX/ZE1k5mqJiXXGbeHSs9efa882qvMkKaMz8WoCSe0nrkwRWw==",
            result);
    }

    [Fact]
    public void GetIp_WithSalt_ChangesHash()
    {
        RobloxIpHasher.SetIpHashSetupForTests(CreateKnownDigitMap(), "end-key");

        var result = RobloxIpHasher.GetIp("127.0.0.1", "pepper");

        Assert.Equal(
            "HNO3ZkpbsEISzAPgSx1/TodfWXtqG04o6YHsAGFXlRwm9Og3UA9pWagBOn/TFkyV0FScQ0E7VrQnm7QB+mR67Q==",
            result);
    }

    [Fact]
    public void GetIp_WhenSetupIsNotInitialized_ThrowsClearError()
    {
        RobloxIpHasher.ResetIpHashSetupForTests();

        var ex = Assert.Throws<InvalidOperationException>(() => RobloxIpHasher.GetIp("127.0.0.1"));

        Assert.Contains("InitializeIpHashSetupAsync", ex.Message);
    }

    private static Dictionary<string, string> CreateKnownDigitMap()
    {
        return new Dictionary<string, string>
        {
            ["0"] = "digit-zero",
            ["1"] = "digit-one",
            ["2"] = "digit-two",
            ["3"] = "digit-three",
            ["4"] = "digit-four",
            ["5"] = "digit-five",
            ["6"] = "digit-six",
            ["7"] = "digit-seven",
            ["8"] = "digit-eight",
            ["9"] = "digit-nine",
        };
    }
}
