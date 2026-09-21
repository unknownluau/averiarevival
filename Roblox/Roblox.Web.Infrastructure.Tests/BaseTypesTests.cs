using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using System.Text;
using Roblox.Services.Exceptions;
using Roblox.Web.Infrastructure.Controllers;
using Roblox.Web.Infrastructure.Http;
using Roblox.Web.Infrastructure.Pages;
using Roblox.Web.Infrastructure.Services;

namespace Roblox.Web.Infrastructure.Tests;

public class BaseTypesTests
{
    [Fact]
    public void ControllerBase_ExposesRequestContextProperties()
    {
        var httpContext = InfrastructureTestHelpers.Context();
        httpContext.Request.Headers["Content-Encoding"] = "gzip";
        httpContext.Request.Headers["Exposed-Credential-Check"] = "4";
        var session = InfrastructureTestHelpers.CreateSession();
        var controller = new TestController
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = httpContext,
            },
            RequestContextAccessor = new StaticRequestContextAccessor(new RobloxRequestContext
            {
                Session = session,
                IsAuthenticated = true,
                IsRcc = true,
                IsRobloxClient = true,
                SessionCookie = "session-cookie",
                DiscordAccessToken = "discord",
                RobloxAccessToken = "roblox",
                RawIp = "127.0.0.1",
                HashedIp = "hash",
                UserAgent = "Roblox/WinInet",
                CurrentGameId = "game",
                CurrentPlaceId = 123,
            }),
            ServicesAccessor = new RobloxServiceAccessor(),
            FileContentCache = new FileContentCache(),
        };

        Assert.Same(session, controller.ExposedUserSession);
        Assert.Same(session, controller.ExposedSafeUserSession);
        Assert.True(controller.ExposedIsGzip);
        Assert.True(controller.ExposedIsPasswordLeaked);
        Assert.True(controller.ExposedIsRcc);
        Assert.True(controller.ExposedIsRoblox);
        Assert.Equal("session-cookie", controller.ExposedSessionCookie);
        Assert.Equal("discord", controller.ExposedDiscordAccessToken);
        Assert.Equal("roblox", controller.ExposedRobloxAccessToken);
        Assert.Equal("Roblox/WinInet", controller.ExposedUserAgent);
        Assert.Equal("game", controller.currentGameId);
        Assert.Equal(123, controller.currentPlaceId);
    }

    [Fact]
    public void ControllerBase_SafeSessionThrowsUnauthorizedWhenMissing()
    {
        var controller = new TestController
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = InfrastructureTestHelpers.Context(),
            },
            RequestContextAccessor = new StaticRequestContextAccessor(new RobloxRequestContext()),
            ServicesAccessor = new RobloxServiceAccessor(),
            FileContentCache = new FileContentCache(),
        };

        var exception = Assert.Throws<RobloxException>(() => controller.ExposedSafeUserSession);

        Assert.Equal(401, exception.statusCode);
        Assert.Equal("Unauthorized", exception.errorMessage);
    }

    [Fact]
    public async Task ControllerBase_GetRequestBodyReturnsParsedFormBody()
    {
        var httpContext = InfrastructureTestHelpers.Context();
        httpContext.Request.ContentType = "application/x-www-form-urlencoded";
        var body = "username=lvm&password=%7D~%295%5B%25Zb%60J%40CENSORED&deviceHandle=02504034d7f9,d85ed30ce3f7,0a0027000008,00155dc804c3";
        httpContext.Request.Body = new MemoryStream(Encoding.UTF8.GetBytes(body));
        httpContext.Request.ContentLength = body.Length;
        var controller = new TestController
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = httpContext,
            },
            RequestContextAccessor = new StaticRequestContextAccessor(new RobloxRequestContext()),
            ServicesAccessor = new RobloxServiceAccessor(),
            FileContentCache = new FileContentCache(),
        };

        var requestBody = await controller.ExposedGetRequestBody();

        Assert.Contains("username=lvm", requestBody);
        Assert.Contains("password=", requestBody);
        Assert.Contains("deviceHandle=", requestBody);
    }

    [Fact]
    public void PageModelBase_ExposesRequestContextProperties()
    {
        var httpContext = InfrastructureTestHelpers.Context();
        httpContext.Request.Headers["Exposed-Credential-Check"] = "4";
        var session = InfrastructureTestHelpers.CreateSession();
        var page = new TestPageModel
        {
            PageContext = new PageContext
            {
                HttpContext = httpContext,
            },
            RequestContextAccessor = new StaticRequestContextAccessor(new RobloxRequestContext
            {
                Session = session,
                IsAuthenticated = true,
                DiscordAccessToken = "discord",
                RobloxAccessToken = "roblox",
                RawIp = "127.0.0.1",
                HashedIp = "hash",
            }),
            ServicesAccessor = new RobloxServiceAccessor(),
        };

        Assert.Same(session, page.userSession);
        Assert.True(page.isAuthenticated);
        Assert.True(page.ExposedIsPasswordLeaked);
        Assert.Equal("discord", page.ExposedDiscordAccessToken);
        Assert.Equal("roblox", page.ExposedRobloxAccessToken);
        Assert.Equal("127.0.0.1", page.ExposedRawIpAddress);
        Assert.Equal("hash", page.ExposedHashedIp);
    }

    private sealed class TestController : RobloxControllerBase
    {
        public Models.Sessions.UserSession? ExposedUserSession => UserSession;
        public Models.Sessions.UserSession ExposedSafeUserSession => SafeUserSession;
        public bool ExposedIsGzip => isGzip;
        public bool ExposedIsPasswordLeaked => isPasswordLeaked;
        public bool ExposedIsRcc => isRCC;
        public bool ExposedIsRoblox => isRoblox;
        public string? ExposedSessionCookie => AVERISECURITY;
        public string? ExposedDiscordAccessToken => discordAccessToken;
        public string? ExposedRobloxAccessToken => robloxAccessToken;
        public string ExposedUserAgent => UserAgent;
        public Task<string> ExposedGetRequestBody()
        {
            return GetRequestBody();
        }
    }

    private sealed class TestPageModel : RobloxPageModelBase
    {
        public bool ExposedIsPasswordLeaked => isPasswordLeaked;
        public string? ExposedDiscordAccessToken => discordAccessToken;
        public string? ExposedRobloxAccessToken => robloxAccessToken;
        public string ExposedRawIpAddress => rawIpAddress;
        public string ExposedHashedIp => hashedIp;
    }

    private sealed class StaticRequestContextAccessor : IRobloxRequestContextAccessor
    {
        public StaticRequestContextAccessor(RobloxRequestContext current)
        {
            Current = current;
        }

        public RobloxRequestContext Current { get; private set; }

        public void SetCurrent(RobloxRequestContext context)
        {
            Current = context;
        }
    }
}
