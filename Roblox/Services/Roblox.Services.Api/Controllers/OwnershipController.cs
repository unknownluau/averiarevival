using Microsoft.AspNetCore.Mvc;
using Roblox.Web.Infrastructure.Controllers;
using Roblox.Web.Infrastructure.Metadata;

namespace Roblox.Services.Api.Controllers;

[ApiController]
[Route("/")]
public class OwnershipController : RobloxControllerBase
{
    [AllowRobloxAnonymous]
    [HttpGet("ownership/hasasset")]
    public async Task<bool> HasAsset(long userId, long assetId)
    {
        var owned = await services.users.GetUserAssets(userId, assetId);
        return owned.Any();
    }
}
