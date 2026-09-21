using System.Net;
using System.Text;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Roblox.Models.Sessions;
using Roblox.Services.Exceptions;
using Roblox.Web.Infrastructure.Http;
using Roblox.Web.Infrastructure.Services;

namespace Roblox.Web.Infrastructure.Controllers;

public class RobloxControllerBase : ControllerBase, IDisposable
{
    [FromServices]
    public RobloxServiceAccessor ServicesAccessor { get; set; } = null!;

    [FromServices]
    public IRobloxRequestContextAccessor RequestContextAccessor { get; set; } = null!;

    [FromServices]
    public FileContentCache FileContentCache { get; set; } = null!;

    protected RobloxServiceAccessor services => ServicesAccessor;

#if DEBUG
    public UserSession? userSessionForTests { get; set; }
#endif

    protected RobloxRequestContext RequestContext => RequestContextAccessor.Current;
    protected UserSession? UserSession => userSession;
    protected UserSession SafeUserSession => safeUserSession;

    protected UserSession? userSession
    {
        get
        {
#if DEBUG
            if (userSessionForTests != null)
            {
                return userSessionForTests;
            }
#endif
            return RequestContext.Session;
        }
    }

    protected UserSession safeUserSession
    {
        get
        {
            if (userSession == null)
            {
                throw new RobloxException(401, 0, "Unauthorized");
            }

            return userSession;
        }
    }

    protected string? AVERISECURITY => RequestContext.SessionCookie;
    protected bool isGzip => Request.Headers["Content-Encoding"].ToString() == "gzip";
    protected string UserAgent => RequestContext.UserAgent;
    protected bool isPasswordLeaked => Request.Headers["Exposed-Credential-Check"].ToString() == "4";
    protected bool isRCC => RequestContext.IsRcc;
    protected bool isRoblox => RequestContext.IsRobloxClient;
    protected string? discordAccessToken => RequestContext.DiscordAccessToken;
    protected string? robloxAccessToken => RequestContext.RobloxAccessToken;
    public string currentGameId => RequestContext.CurrentGameId;
    public long currentPlaceId => RequestContext.CurrentPlaceId;

    public static string GetRequesterIpRaw(HttpContext ctx)
    {
        return RobloxIpHasher.GetRequesterIpRaw(ctx);
    }

    public static Task InitializeIpHashSetupAsync()
    {
        return RobloxIpHasher.InitializeIpHashSetupAsync();
    }

    public static ulong ConvertFromIpAddressToInteger(string ipAddress)
    {
        return RobloxIpHasher.ConvertFromIpAddressToInteger(ipAddress);
    }

    public static string GetIP(string trueIp, string? salt = null)
    {
        return RobloxIpHasher.GetIp(trueIp, salt);
    }

    protected async Task<string> GetRequestBody()
    {
        if (Request.HasFormContentType)
        {
            var form = await Request.ReadFormAsync();
            return string.Join("&", form.SelectMany(field => field.Value.Select(value =>
                $"{WebUtility.UrlEncode(field.Key)}={WebUtility.UrlEncode(value)}")));
        }

        using var reader = new StreamReader(Request.Body, Encoding.UTF8, leaveOpen: false);
        return await reader.ReadToEndAsync();
    }

    protected async Task<MemoryStream> GetRequestBodyAsMemoryStream()
    {
        var ms = new MemoryStream();
        await Request.Body.CopyToAsync(ms);
        ms.Position = 0;
        return ms;
    }

    protected string GetIpHash(string? salt = null)
    {
        return GetIP(salt);
    }

    protected string GetIP(string? salt = null)
    {
        if (!string.IsNullOrWhiteSpace(RequestContext.RawIp))
        {
            return RobloxIpHasher.GetIp(RequestContext.RawIp, salt);
        }

        return string.IsNullOrWhiteSpace(salt) ? RequestContext.HashedIp : string.Empty;
    }

    public void Dispose()
    {
        ServicesAccessor?.Dispose();
    }
}
