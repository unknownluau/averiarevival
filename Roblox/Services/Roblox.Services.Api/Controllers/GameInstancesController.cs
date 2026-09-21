using Microsoft.AspNetCore.Mvc;
using Roblox.Services.Exceptions;
using Roblox.Web.Infrastructure.Controllers;
using Roblox.Web.Infrastructure.Metadata;
using Roblox.Dto.Games;
namespace Roblox.Services.Api.Controllers;

[ApiController]
[Route("/")]
public class GameInstancesController : RobloxControllerBase
{
    [RequireRccRequest]
    [HttpGet("v1/Close")]
    [HttpPost("V1/Close")]
    public async Task<IActionResult> Close(Guid gameId)
    {
        if (!isRCC)
        {
            throw new RobloxException(401, 0, "Unauthorized");
        }

        await services.gameServer.ShutDownServerAsync(gameId);
        return Ok("OK");
    }

    [RequireRccRequest]
    [HttpPost("v2/CreateOrUpdate")]
    [HttpGet("v2/CreateOrUpdate")]
    [HttpGet("v1/CreateOrUpdate")]
    [HttpPost("v1/CreateOrUpdate")]
    public async Task<IActionResult> CreateOrUpdate(string gameId, decimal ping, decimal fps)
    {
        if (!isRCC)
        {
            throw new RobloxException(401, 0, "Unauthorized");
        }

        var roundPing = (int)Math.Round(ping, 0);
        var roundFps = (int)Math.Round(fps, 0);
        await services.gameServer.SetServerStats(gameId, roundPing, roundFps);
        return Ok("OK!");
    }

    [RequireRccRequest]
    [HttpPost("v1.0/Refresh")]
    [HttpPost("v2.0/Refresh")]
    [HttpGet("v1.0/Refresh")]
    [HttpGet("v2.0/Refresh")]
    public async Task Refresh(Guid gameId, long clientCount, decimal gameTime)
    {
        if (!isRCC)
        {
            throw new RobloxException(401, 0, "Unauthorized");
        }

        if (clientCount == 0 && gameTime > 50)
        {
            await services.gameServer.ShutDownServerAsync(gameId);
            return;
        }

        await services.gameServer.SetServerPing(gameId);
    }
}
