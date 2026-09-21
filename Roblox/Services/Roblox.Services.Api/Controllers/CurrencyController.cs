using Microsoft.AspNetCore.Mvc;
using Roblox.Models.Assets;
using Roblox.Dto.Users;
using Roblox.Web.Infrastructure.Controllers;
using Roblox.Web.Infrastructure.Metadata;

namespace Roblox.Services.Api.Controllers;

[ApiController]
[Route("/")]
public class CurrencyController : RobloxControllerBase
{
    [RequireRobloxSession]
    [HttpGet("currency/balance")]
    public async Task<UserEconomy> GetBalance()
    {
        return await services.economy.GetBalance(CreatorType.User, safeUserSession.userId);
    }
}
