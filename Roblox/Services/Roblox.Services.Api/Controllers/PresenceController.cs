using Microsoft.AspNetCore.Mvc;
using Roblox.Models.Users;
using Roblox.Services;
using Roblox.Services.Exceptions;
using Roblox.Web.Infrastructure.Controllers;
using Roblox.Web.Infrastructure.Metadata;

namespace Roblox.Services.Api.Controllers;

[ApiController]
[Route("/")]
public class PresenceController : RobloxControllerBase
{
    [RequireRccRequest]
    [HttpPost("presence/register-game-presence")]
    public async Task<IActionResult> RegisterGamePresence(long visitorId, long placeId, Guid gameId, string locationType)
    {
        if (!isRCC || placeId != currentPlaceId || gameId.ToString() != currentGameId)
        {
            throw new RobloxException(401, 0, "Unauthorized");
        }

        if (!await services.playerSecurity.IsPlayerTicketValid(visitorId, gameId))
        {
            await services.gameServer.KickPlayer(visitorId, gameId);
            await services.discordBotApi.SendMessageInChannel(Roblox.Configuration.DiscordLogChannelId, $"[RAGE-SS] UID: {visitorId} Flag: PlayerSpoofer");
            throw new RobloxException(403, 0, "User does not have a valid placelauncher ticket");
        }

        var onlineStatus = (await services.users.MultiGetPresence(new[] { visitorId })).First();
        var hasSuspiciousLastOnline = onlineStatus.lastOnline < DateTime.UtcNow.Subtract(TimeSpan.FromMinutes(2)) ||
                                      onlineStatus.userPresenceType == PresenceType.Offline;
        if (hasSuspiciousLastOnline)
        {
            await services.discordBotApi.SendMessageInChannel(Roblox.Configuration.DiscordLogChannelId, $"[RAGE-SS] UID: {visitorId} Flag: SuspiciousLastOnline");
        }

        var gameServer = await services.gameServer.GetGameServer(gameId);
        if (placeId != gameServer.assetId)
        {
            throw new RobloxException(400, 0, "BadRequest");
        }

        var userInfo = await services.users.GetUserById(visitorId);
        if (userInfo.IsDeleted())
        {
            await services.gameServer.KickPlayer(visitorId, gameId);
            await services.discordBotApi.SendMessageInChannel(Roblox.Configuration.DiscordLogChannelId, $"[RAGE-SS] UID: {visitorId} Flag: BannedUser");
            throw new RobloxException(403, 0, "User is banned");
        }

        await services.gameServer.OnPlayerJoin(visitorId, placeId, gameId);
        return Ok();
    }

    [RequireRccRequest]
    [HttpPost("presence/register-absence")]
    public async Task RegisterAbsence(long visitorId)
    {
        if (!isRCC)
        {
            throw new RobloxException(401, 0, "Unauthorized");
        }

        var jobId = await services.gameServer.GetJobIdByUserId(visitorId);

        var placeId = GameServerService.GetUserPlaceId(visitorId);

        await services.gameServer.OnPlayerLeave(visitorId, placeId, jobId);
    }
}
