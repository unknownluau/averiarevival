using System.Net;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;
using Roblox.Web.Infrastructure.Auth;
using Roblox.Models.Sessions;
using Roblox.Models.Users;
using Roblox.Web.Infrastructure.Configuration;
using Roblox.Web.Infrastructure.Http;
using Roblox.Web.Infrastructure.Metadata;
using Roblox.Web.Infrastructure.Middleware;
using Roblox.Website.Middleware;

namespace Roblox.Web.Infrastructure.Tests;

internal static class InfrastructureTestHelpers
{
    static InfrastructureTestHelpers()
    {
        TryConfigureCsrf();
        TryConfigureSessionJwt();
    }

    public static Endpoint Endpoint(params object[] metadata)
    {
        return new Endpoint(_ => Task.CompletedTask, new EndpointMetadataCollection(metadata), "test");
    }

    public static DefaultHttpContext Context(Endpoint? endpoint = null)
    {
        var context = new DefaultHttpContext();
        context.Response.Body = new MemoryStream();
        context.Connection.RemoteIpAddress = IPAddress.Parse("127.0.0.1");
        context.Request.Headers[RobloxWebContextConstants.ClientIpHashHeaderName] = "test-client-ip-hash";
        if (endpoint != null)
        {
            context.SetEndpoint(endpoint);
        }

        return context;
    }

    public static UserSession CreateSession(
        long userId = 123,
        string username = "InfrastructureUser",
        string sessionId = "session-id",
        AccountStatus accountStatus = AccountStatus.Ok)
    {
        return new UserSession(userId, username, DateTime.UtcNow, accountStatus, 0, false, sessionId);
    }

    public static void AddForwardedSessionHeaders(DefaultHttpContext context, UserSession? session = null)
    {
        session ??= CreateSession();
        context.Request.Headers[RobloxWebContextConstants.UserIdHeaderName] = session.userId.ToString();
        context.Request.Headers[RobloxWebContextConstants.UsernameHeaderName] = session.username;
        context.Request.Headers[RobloxWebContextConstants.SessionIdHeaderName] = session.sessionId;
        context.Request.Headers[RobloxWebContextConstants.AccountStatusHeaderName] = session.accountStatus.ToString();
    }

    public static void AddCookie(DefaultHttpContext context, string name, string value)
    {
        context.Request.Headers.Cookie = $"{name}={Uri.EscapeDataString(value)}";
    }

    public static IOptions<RobloxWebInfrastructureOptions> Options(Action<RobloxWebInfrastructureOptions>? configure = null)
    {
        var options = new RobloxWebInfrastructureOptions
        {
            Authorization = TestConstants.ProxyAuthorization,
            RccAuthorization = TestConstants.RccAuthorization,
            SessionJwtKey = TestConstants.SessionJwtKey,
        };
        configure?.Invoke(options);
        return Microsoft.Extensions.Options.Options.Create(options);
    }

    public static RobloxRequestContextAccessor RequestContextAccessor(DefaultHttpContext context, IOptions<RobloxWebInfrastructureOptions>? options = null)
    {
        return new RobloxRequestContextAccessor(
            new HttpContextAccessor
            {
                HttpContext = context,
            },
            options ?? Options());
    }

    public static async Task<(DefaultHttpContext Context, bool NextCalled)> InvokeProxyForwardedAuthAsync(
        Endpoint endpoint,
        Action<DefaultHttpContext>? configure = null)
    {
        var nextCalled = false;
        var context = Context(endpoint);
        configure?.Invoke(context);
        var options = Options();
        var middleware = new ProxyForwardedAuthMiddleware(_ =>
        {
            nextCalled = true;
            return Task.CompletedTask;
        }, options);

        await middleware.InvokeAsync(context, RequestContextAccessor(context, options));
        return (context, nextCalled);
    }

    public static async Task<(DefaultHttpContext Context, bool NextCalled)> InvokeApiProxyForwardedAuthAsync(
        Action<RobloxWebInfrastructureOptions> configureOptions,
        Action<DefaultHttpContext>? configureContext = null)
    {
        var nextCalled = false;
        var context = Context();
        context.Request.Host = new HostString("www.test.local");
        context.Request.Path = "/v1/test";
        context.Request.Headers.UserAgent = "Mozilla/5.0";
        context.Request.Headers[RobloxWebContextConstants.ClientIpHashHeaderName] = "forwarded-hash";
        configureContext?.Invoke(context);

        var options = Options(configureOptions);
        var middleware = new ApiProxyForwardedAuthMiddleware(_ =>
        {
            nextCalled = true;
            return Task.CompletedTask;
        }, options);

        await middleware.InvokeAsync(context, RequestContextAccessor(context, options));
        return (context, nextCalled);
    }

    public static async Task<(DefaultHttpContext Context, bool NextCalled)> InvokeApplicationGuardAsync(
        Endpoint endpoint,
        Action<DefaultHttpContext>? configure = null)
    {
        var nextCalled = false;
        var context = Context(endpoint);
        context.Request.Headers.UserAgent = "Mozilla/5.0";
        context.SetRobloxRequestContext(RobloxRequestContextFactory.CreateAnonymous(context, TestConstants.RccAuthorization));
        configure?.Invoke(context);

        ApplicationGuardMiddleware.Configure(TestConstants.ProxyAuthorization);
        var middleware = new ApplicationGuardMiddleware(_ =>
        {
            nextCalled = true;
            return Task.CompletedTask;
        });

        await middleware.InvokeAsync(context);
        return (context, nextCalled);
    }

    public static async Task<(DefaultHttpContext Context, bool NextCalled)> InvokeCsrfAsync(
        Endpoint endpoint,
        string method,
        Action<DefaultHttpContext>? configure = null)
    {
        var nextCalled = false;
        var context = Context(endpoint);
        context.Request.Scheme = "https";
        context.Request.Host = new HostString("www.test.local");
        context.Request.Path = "/csrf-test";
        context.Request.Method = method;
        configure?.Invoke(context);

        var middleware = new CsrfMiddleware(_ =>
        {
            nextCalled = true;
            return Task.CompletedTask;
        });

        await middleware.InvokeAsync(context);
        return (context, nextCalled);
    }

    public static string CreateCsrfToken(string csrf, DateTime? createdAt = null)
    {
        TryConfigureCsrf();
        return CsrfMiddleware.CreateJwt(new CsrfJwtEntry
        {
            csrf = csrf,
            createdAt = createdAt ?? DateTime.UtcNow,
        });
    }

    public static string CreateSessionCookie(string sessionId)
    {
        TryConfigureSessionJwt();
        return RobloxSessionTokenCodec.CreateJwt(new SessionTokenPayload
        {
            sessionId = sessionId,
            createdAt = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
        });
    }

    public static async Task<JsonDocument> ReadJsonAsync(DefaultHttpContext context)
    {
        context.Response.Body.Position = 0;
        return await JsonDocument.ParseAsync(context.Response.Body);
    }

    public static string ReadBody(DefaultHttpContext context)
    {
        context.Response.Body.Position = 0;
        using var reader = new StreamReader(context.Response.Body, Encoding.UTF8, leaveOpen: true);
        return reader.ReadToEnd();
    }

    public static void TryConfigureCsrf()
    {
        try
        {
            CsrfMiddleware.Configure(TestConstants.CsrfJwtKey);
        }
        catch
        {
            // The legacy middleware has process-wide one-time configuration.
        }
    }

    public static void TryConfigureSessionJwt()
    {
        try
        {
            RobloxSessionTokenCodec.Configure(TestConstants.SessionJwtKey);
        }
        catch
        {
            // The session codec has process-wide one-time configuration.
        }
    }
}
