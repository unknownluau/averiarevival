using Microsoft.AspNetCore.Mvc;
using Roblox.Dto.Authentication;
using Roblox.Web.Infrastructure.Auth;
using Roblox.Web.Infrastructure.Controllers;
using Roblox.Web.Infrastructure.Metadata;

namespace Roblox.Services.Api.Controllers;

[ApiController]
[Route("/")]
public class AuthenticationController : RobloxControllerBase
{
    [AllowRobloxAnonymous]
    [RequireRobloxClient]
    [HttpPost("v1/login")]
    public async Task<LoginV1Response> LoginV1([FromBody] LoginRequest request)
    {
        var result = await services.authentication.LoginV1(request, CreateLoginRequestContext());
        RobloxSessionCookieWriter.AppendSessionCookies(HttpContext, result.sessionId);
        return result.response;
    }

    [AllowRobloxAnonymous]
    [HttpPost("v2/login")]
    public async Task<IActionResult> LoginV2()
    {
        var result = await services.authentication.LoginV2(await GetRequestBody(), CreateLoginRequestContext());

        if (result.requiresTwoStepVerification)
        {
            return Ok(result.twoStepVerificationResponse);
        }

        RobloxSessionCookieWriter.AppendSessionCookies(HttpContext, result.sessionId!);
        return Ok(result.response);
    }
    
    [RequireRobloxSession]
    [HttpPost("sign-out/v1")]
    public void Logout()
    {
        using var sessCache = Roblox.Services.ServiceProvider.GetOrCreate<UserSessionsCache>();
        sessCache.Remove(safeUserSession.sessionId);
        RobloxSessionCookieWriter.DeleteSessionCookies(HttpContext);
    }

    private LoginRequestContext CreateLoginRequestContext()
    {
        return new LoginRequestContext
        {
            hashedIp = GetIpHash(),
            userAgent = UserAgent,
            isRobloxClient = isRoblox,
            isPasswordLeaked = isPasswordLeaked,
        };
    }
}
