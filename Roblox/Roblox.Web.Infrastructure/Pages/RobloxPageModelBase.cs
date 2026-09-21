using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Roblox.Models.Sessions;
using Roblox.Web.Infrastructure.Http;
using Roblox.Web.Infrastructure.Services;

namespace Roblox.Web.Infrastructure.Pages;

public class RobloxPageModelBase : PageModel, IDisposable
{
    [FromServices]
    public RobloxServiceAccessor ServicesAccessor { get; set; } = null!;

    [FromServices]
    public IRobloxRequestContextAccessor RequestContextAccessor { get; set; } = null!;

    protected RobloxServiceAccessor services => ServicesAccessor;

    protected RobloxRequestContext RequestContext => RequestContextAccessor.Current;
    public UserSession? userSession => RequestContext.Session;
    public bool isAuthenticated => userSession != null;
    protected bool isPasswordLeaked => Request.Headers["Exposed-Credential-Check"].ToString() == "4";
    protected string? discordAccessToken => RequestContext.DiscordAccessToken;
    protected string? robloxAccessToken => RequestContext.RobloxAccessToken;
    protected string rawIpAddress => RequestContext.RawIp;
    protected string hashedIp => RequestContext.HashedIp;
    public string nonce { get; set; } = string.Empty;

    protected string GetIpHashWithSalt(string salt)
    {
        if (string.IsNullOrWhiteSpace(rawIpAddress))
        {
            return string.Empty;
        }

        return RobloxIpHasher.GetIp(rawIpAddress, salt);
    }

    public virtual void Dispose()
    {
        ServicesAccessor?.Dispose();
    }
}
