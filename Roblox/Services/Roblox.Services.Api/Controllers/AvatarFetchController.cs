using Microsoft.AspNetCore.Mvc;
using Roblox.Web.Infrastructure.Controllers;
using Roblox.Web.Infrastructure.Metadata;
using Type = Roblox.Models.Assets.Type;

namespace Roblox.Services.Api.Controllers;

[ApiController]
[Route("/")]
public class AvatarFetchController : RobloxControllerBase
{
    // TODO: separate v1 and v1.1
    [AllowRobloxAnonymous]
    [HttpGet("/v1/avatar-fetch")]
    [HttpGet("/v1.1/avatar-fetch")]
    public async Task<IActionResult> AvatarFetch(long? placeId, long userId)
    {
        List<long> accessoryVersionIds = new();
        List<long> equippedGearVersionIds = new();
        var wornAssets = await services.avatar.GetWornAssets(userId);
        var avatar = await services.avatar.GetAvatar(userId);
        var assetInfo = await services.assets.MultiGetInfoById(wornAssets);
        var bodyColors = new Dictionary<string, int>
        {
            ["headColorId"] = avatar.headColorId,
            ["leftArmColorId"] = avatar.leftArmColorId,
            ["leftLegColorId"] = avatar.leftLegColorId,
            ["rightArmColorId"] = avatar.rightArmColorId,
            ["rightLegColorId"] = avatar.rightLegColorId,
            ["torsoColorId"] = avatar.torsoColorId,
            ["HeadColor"] = avatar.headColorId,
            ["LeftArmColor"] = avatar.leftArmColorId,
            ["LeftLegColor"] = avatar.leftLegColorId,
            ["RightArmColor"] = avatar.rightArmColorId,
            ["RightLegColor"] = avatar.rightLegColorId,
            ["TorsoColor"] = avatar.torsoColorId,
        };
        var scales = new Dictionary<string, double>
        {
            ["height"] = avatar.scales.height,
            ["Height"] = avatar.scales.height,
            ["width"] = avatar.scales.width,
            ["Width"] = avatar.scales.width,
            ["head"] = avatar.scales.head,
            ["Head"] = avatar.scales.head,
            ["depth"] = avatar.scales.depth,
            ["Depth"] = avatar.scales.depth,
            ["proportion"] = avatar.scales.proportion,
            ["Proportion"] = avatar.scales.proportion,
            ["bodyType"] = avatar.scales.bodyType,
            ["BodyType"] = avatar.scales.bodyType,
        };

        equippedGearVersionIds.AddRange(assetInfo.Where(d => d.assetType == Type.Gear).Select(d => d.id));
        accessoryVersionIds.AddRange(assetInfo.Where(d => (d.assetType != Type.Gear && placeId != 0) && d.assetType != Type.EmoteAnimation).Select(d => d.id));
        if (placeId != 0)
        {
            equippedGearVersionIds = new List<long>();
        }

        int positionCounter = 1;
        var animationAssetIds = assetInfo
            .Where(c => c.assetType is Type.RunAnimation or Type.JumpAnimation or Type.FallAnimation or Type.ClimbAnimation or Type.IdleAnimation or Type.WalkAnimation or Type.SwimAnimation)
            .GroupBy(c => c.assetType.ToString().Replace("Animation", "").ToLower())
            .ToDictionary(g => g.Key, g => g.First().id);

        var result = new
        {
            resolvedAvatarType = avatar.avatarType.ToString(),
            accessoryVersionIds,
            equippedGearVersionIds,
            assetAndAssetTypeIds = assetInfo
                .Where(c => c.assetType != Type.EmoteAnimation && !animationAssetIds.ContainsKey(c.assetType.ToString().Replace("Animation", "").ToLower()))
                .Select(c => new
                {
                    assetId = c.id,
                    assetTypeId = (int)c.assetType,
                }),
            backpackGearVersionIds = equippedGearVersionIds,
            animationAssetIds,
            playerAvatarType = avatar.avatarType.ToString(),
            scales,
            bodyColorsUrl = $"{Roblox.Configuration.BaseUrl}/Asset/BodyColors.ashx?userId={userId}",
            bodyColors,
            emotes = assetInfo.Where(c => c.assetType == Type.EmoteAnimation).Select(c => new
            {
                assetId = c.id,
                assetName = c.name,
                position = positionCounter++,
            }),
        };

        return new JsonResult(result);
    }
}