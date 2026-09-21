using Microsoft.AspNetCore.Mvc;
using Roblox.Web.Infrastructure.Controllers;
using Roblox.Web.Infrastructure.Metadata;

namespace Roblox.Services.Api.Controllers;

[ApiController]
[Route("/")]
public class ModerationController : RobloxControllerBase
{
    [HttpPost("moderation/v2/filtertext/")]
    [HttpPost("moderation/filtertext/")]
    [AllowRobloxAnonymous]
    [BrowserFacingEndpoint]
    public dynamic FilterText()
    {
        var text = services.filter.FilterText(HttpContext.Request.Form["text"].ToString());
        return new
        {
            success = true,
            data = new
            {
                AgeUnder13 = text,
                Age13OrOver = text,
                white = text,
                black = text,
            },
        };
    }
}
