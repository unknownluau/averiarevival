using System.ComponentModel.DataAnnotations;
using System.Reflection;
using Microsoft.AspNetCore.Mvc;
using Roblox.Dto.Avatar;
using Roblox.Logging;
using Roblox.Models.Avatar;
using Roblox.Services;
using Roblox.Services.App.FeatureFlags;
using Roblox.Services.Exceptions;
using Roblox.Web.Infrastructure.Controllers;
using Roblox.Web.Infrastructure.Http;
using Roblox.Web.Infrastructure.Metadata;
using AssetType = Roblox.Models.Assets.Type;
using ServiceProvider = Roblox.Services.ServiceProvider;

namespace Roblox.Services.Avatar.Controllers;

[ApiController]
[Route("/")]
public class AvatarController : RobloxControllerBase
{
    private void FeatureCheck()
    {
        FeatureFlags.FeatureCheck(FeatureFlag.AvatarsEnabled);
    }

    private void AttemptScheduleRender(bool forceRedraw = false)
    {
        if (userSession == null)
            return;

        QueueAvatarRender(safeUserSession.userId, forceRedraw);
    }

    private static void QueueAvatarRender(long userId, bool forceRedraw)
    {
        if (ExecutionContext.IsFlowSuppressed())
        {
            _ = Task.Run(() => AttemptScheduleRenderAsync(userId, forceRedraw));
            return;
        }

        using (ExecutionContext.SuppressFlow())
        {
            _ = Task.Run(() => AttemptScheduleRenderAsync(userId, forceRedraw));
        }
    }

    private static async Task AttemptScheduleRenderAsync(long userId, bool forceRedraw, int attempt = 0)
    {
        var cache = ServiceProvider.GetOrCreate<AvatarCache>();
        if (!forceRedraw)
        {
            if (!cache.AttemptScheduleRender(userId))
            {
                Writer.Info(LogGroup.AvatarService, "Render already scheduled for user {0}", userId);
                return;
            }
        }
        else
        {
            await cache.DeleteAvatarCache(userId);
        }

        try
        {
            using var avatarService = ServiceProvider.GetOrCreate<AvatarService>();
            AvatarType? rigType = (AvatarType?)await avatarService.GetAvatarType(userId);
            var assetIds = await cache.GetAndClearPendingAssets(userId);
            var newColors = await cache.GetAndClearColors(userId);
            const bool skipRender = false;
            const bool skipLock = false;
            await avatarService.RedrawAvatar(userId, assetIds, newColors, rigType, forceRedraw, skipLock, skipRender);
        }
        catch (LockNotAcquiredException) when (attempt < 5)
        {
            Writer.Info(LogGroup.AvatarService, "Avatar render lock busy for user {0}, retry attempt {1}", userId, attempt + 1);
            await Task.Delay(TimeSpan.FromSeconds(2));
            await AttemptScheduleRenderAsync(userId, forceRedraw, attempt + 1);
            return;
        }
        catch (Exception e)
        {
            Console.WriteLine("Background render failed: {0}\n{1}", e.Message, e.StackTrace);
        }
        finally
        {
            cache.UnscheduleRender(userId);
        }

        if (!forceRedraw && await cache.HasPendingAvatarChanges(userId))
        {
            await AttemptScheduleRenderAsync(userId, false);
        }
    }

    // TODO: does this even belong in avatar and WWW?
    [AllowRobloxAnonymous]
    [HttpGet("/v1/avatar-fetch")]
    [HttpGet("/v1.1/avatar-fetch")]
    public async Task<IActionResult> CharacterFetch(long? placeId, long userId)
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

        equippedGearVersionIds.AddRange(assetInfo.Where(d => d.assetType == AssetType.Gear).Select(d => d.id));
        accessoryVersionIds.AddRange(assetInfo.Where(d => (d.assetType != AssetType.Gear && placeId != 0) && d.assetType != AssetType.EmoteAnimation).Select(d => d.id));
        if (placeId != 0)
        {
            equippedGearVersionIds = new List<long>();
        }

        int positionCounter = 1;
        var animationAssetIds = assetInfo
            .Where(c => c.assetType is AssetType.RunAnimation or AssetType.JumpAnimation or AssetType.FallAnimation or AssetType.ClimbAnimation or AssetType.IdleAnimation or AssetType.WalkAnimation or AssetType.SwimAnimation)
            .GroupBy(c => c.assetType.ToString().Replace("Animation", "").ToLower())
            .ToDictionary(g => g.Key, g => g.First().id);

        var result = new
        {
            resolvedAvatarType = avatar.avatarType.ToString(),
            accessoryVersionIds,
            equippedGearVersionIds,
            assetAndAssetTypeIds = assetInfo
                .Where(c => c.assetType != AssetType.EmoteAnimation && !animationAssetIds.ContainsKey(c.assetType.ToString().Replace("Animation", "").ToLower()))
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
            emotes = assetInfo.Where(c => c.assetType == AssetType.EmoteAnimation).Select(c => new
            {
                assetId = c.id,
                assetName = c.name,
                position = positionCounter++,
            }),
        };

        return new JsonResult(result);
    }

    [RequireRobloxSession]
    [HttpPost("/v1/avatar/redraw-thumbnail")]
    [HttpPost("/apisite/avatar/v1/avatar/redraw-thumbnail")]
    public void RequestRedrawAvatar()
    {
        FeatureCheck();
        AttemptScheduleRender(true);
    }

    [RequireRobloxSession]
    [HttpPost("/v1/avatar/set-wearing-assets")]
    [HttpPost("/apisite/avatar/v1/avatar/set-wearing-assets")]
    public async Task SetWornAssets([Required, FromBody] SetWearingAssetsRequest request)
    {
        FeatureCheck();

        var currentlyWorn = (await services.avatar.GetWornAssets(safeUserSession.userId)).ToList();
        var newAssetIds = request.assetIds.ToList();
        Writer.Info(LogGroup.AvatarService, "SetWornAssets current = {0} new = {1}", System.Text.Json.JsonSerializer.Serialize(currentlyWorn), System.Text.Json.JsonSerializer.Serialize(newAssetIds));
        var changedAssetIds = currentlyWorn.Except(newAssetIds).Concat(newAssetIds.Except(currentlyWorn)).ToList();
        Writer.Info(LogGroup.AvatarService, "Changed assets = {0}", System.Text.Json.JsonSerializer.Serialize(changedAssetIds));

        await services.avatar.SetWearingAssets(safeUserSession.userId, newAssetIds);
        AttemptScheduleRender(true);

        foreach (long assetId in changedAssetIds)
        {
            await services.avatar.UpdateLastUpdated(safeUserSession.userId, assetId);
        }
    }

    [RequireRobloxSession]
    [HttpPost("/v1/avatar/assets/{assetId:long}/wear")]
    [HttpPost("/apisite/avatar/v1/avatar/assets/{assetId:long}/wear")]
    public async Task WearAsset([Required] long assetId)
    {
        FeatureCheck();
        var currentlyWorn = (await services.avatar.GetWornAssets(safeUserSession.userId)).ToList();
        if (!currentlyWorn.Contains(assetId))
        {
            currentlyWorn.Add(assetId);
        }

        using var cache = ServiceProvider.GetOrCreate<AvatarCache>();
        await cache.SetPendingAssets(safeUserSession.userId, currentlyWorn);
        await services.avatar.UpdateLastUpdated(safeUserSession.userId, assetId);
        await services.avatar.UpdateUserAvatarImages(safeUserSession.userId, null, null, null);
        AttemptScheduleRender();
    }

    [RequireRobloxSession]
    [HttpPost("/v1/avatar/assets/{assetId:long}/remove")]
    [HttpPost("/apisite/avatar/v1/avatar/assets/{assetId:long}/remove")]
    public async Task RemoveAsset([Required] long assetId)
    {
        FeatureCheck();
        var currentlyWorn = (await services.avatar.GetWornAssets(safeUserSession.userId)).ToList();
        if (!currentlyWorn.Contains(assetId))
        {
            Writer.Info(LogGroup.AvatarService, "User {0} tried to remove asset {1} but it was not worn", safeUserSession.userId, assetId);
            return;
        }

        currentlyWorn.Remove(assetId);
        using var cache = ServiceProvider.GetOrCreate<AvatarCache>();
        await cache.SetPendingAssets(safeUserSession.userId, currentlyWorn);
        await services.avatar.UpdateLastUpdated(safeUserSession.userId, assetId);
        await services.avatar.UpdateUserAvatarImages(safeUserSession.userId, null, null, null);
        AttemptScheduleRender();
    }

    [RequireRobloxSession]
    [HttpPost("/v1/avatar/set-scales")]
    [HttpPost("/apisite/avatar/v1/avatar/set-scales")]
    public async Task SetBodyScales([Required, FromBody] BodyScales request)
    {
        if (!services.avatar.AreScalesValid(request) && safeUserSession.userId is not (68 or 3))
            throw BadRequest("One or more scales are out of bounds.");

        await services.avatar.UpdateBodyScales(request, safeUserSession.userId);
        await services.avatar.UpdateUserAvatarImages(safeUserSession.userId, null, null, null);
        AttemptScheduleRender();
    }

    [RequireRobloxSession]
    [HttpPost("/v1/avatar/set-player-avatar-type")]
    [HttpPost("/apisite/avatar/v1/avatar/set-player-avatar-type")]
    public async Task SetBodyRigType([Required, FromBody] SetAvatarTypeRequest request)
    {
        if (!Enum.IsDefined(typeof(AvatarType), request.playerAvatarType))
            throw BadRequest("Invalid player avatar type");

        await services.avatar.UpdateRigType(request.playerAvatarType, safeUserSession.userId);
        await services.avatar.UpdateUserAvatarImages(safeUserSession.userId, null, null, null);
        AttemptScheduleRender();
    }
    
    [RequireRobloxSession]
    [HttpGet("/v1/avatar/set-rig")]
    [HttpGet("/apisite/avatar/v1/avatar/set-rig")]
    public async Task SetBodyRigType([Required] string rigType)
    {
        if (!Enum.TryParse(rigType, ignoreCase: true, out AvatarType avatarRigType))
            throw BadRequest("Invalid player avatar type");
        
        var userId =  safeUserSession.userId;

        await services.avatar.UpdateRigType(avatarRigType, userId);
        await services.avatar.UpdateUserAvatarImages(userId, null, null, null);
        AttemptScheduleRender();
    }

    [RequireRobloxSession]
    [HttpPost("/v1/avatar/set-body-colors")]
    [HttpPost("/apisite/avatar/v1/avatar/set-body-colors")]
    public async Task SetBodyColors([Required, FromBody] ColorEntry colors)
    {
        FeatureCheck();
        var userId = safeUserSession.userId;
        var validColorIds = Models.Avatar.AvatarMetadata.GetColors().Select(color => color.brickColorId).ToHashSet();
        if (!validColorIds.Contains(colors.headColorId) ||
            !validColorIds.Contains(colors.torsoColorId) ||
            !validColorIds.Contains(colors.leftArmColorId) ||
            !validColorIds.Contains(colors.rightArmColorId) ||
            !validColorIds.Contains(colors.leftLegColorId) ||
            !validColorIds.Contains(colors.rightLegColorId))
        {
            throw BadRequest("Invalid body color(s).");
        }

        using var cache = ServiceProvider.GetOrCreate<AvatarCache>();
        await cache.SetColors(userId, colors);
        await services.avatar.UpdateUserAvatarImages(userId, null, null, null);
        AttemptScheduleRender();
    }

    [RequireRobloxSession]
    [HttpGet("/v1/recent-items/{recentType}/list")]
    [HttpGet("/apisite/avatar/v1/recent-items/{recentType}/list")]
    public async Task<dynamic> GetRecentItems([Required] string recentType)
    {
        FeatureCheck();

        var prop = typeof(AvatarService.AssetTypeGroups).GetProperty(recentType, BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase);
        int[]? assetTypes = prop?.GetValue(services.avatar.recentAssetTypes) as int[];
        if (assetTypes == null)
            throw BadRequest("Bad Recent Type path parameter");

        var recent = (await services.avatar.GetRecentAvatarItems(safeUserSession.userId, assetTypes)).ToList();
        var multiGet = await services.assets.MultiGetInfoById(recent);
        return new
        {
            data = multiGet.OrderBy(e => recent.IndexOf(e.id)).Select(c => new
            {
                c.id,
                c.name,
                type = "Asset",
                assetType = new
                {
                    id = (int)c.assetType,
                    name = c.assetType,
                }
            })
        };
    }

    [AllowRobloxAnonymous]
    [HttpGet("/v1/users/{userId:long}/outfits")]
    [HttpGet("/apisite/avatar/v1/users/{userId:long}/outfits")]
    public async Task<dynamic> GetUserOutfits(long userId, int itemsPerPage, int page)
    {
        FeatureCheck();
        var offset = itemsPerPage * page - itemsPerPage;
        var result = (await services.avatar.GetUserOutfits(userId, itemsPerPage, offset)).ToList();
        return new
        {
            filteredCount = 0,
            data = result,
            total = result.Count,
        };
    }

    [RequireRobloxSession]
    [HttpPost("/v1/outfits/{outfitId:long}/wear")]
    [HttpPost("/apisite/avatar/v1/outfits/{outfitId:long}/wear")]
    public async Task WearOutfit(long outfitId)
    {
        FeatureCheck();
        var outfitDetails = await services.avatar.GetOutfitById(outfitId);
        var scales = new BodyScales
        {
            height = outfitDetails.details.height,
            width = outfitDetails.details.width,
            head = outfitDetails.details.head,
            depth = outfitDetails.details.depth,
            bodyType = outfitDetails.details.bodyType,
            proportion = outfitDetails.details.proportion,
        };
        using var avatarCache = ServiceProvider.GetOrCreate<AvatarCache>();
        await avatarCache.DeleteAvatarCache(safeUserSession.userId);
        await services.avatar.RedrawAvatar(safeUserSession.userId, outfitDetails.assetIds, outfitDetails.details, outfitDetails.details.avatarType, false, false, false, scales);
    }

    [RequireRobloxSession]
    [HttpPost("/v1/outfits/create")]
    [HttpPost("/apisite/avatar/v1/outfits/create")]
    public async Task CreateOutfit([Required, FromBody] CreateOutfitRequest request)
    {
        FeatureCheck();
        var assets = await services.avatar.GetWornAssets(safeUserSession.userId);
        var existingAvatar = await services.avatar.GetAvatar(safeUserSession.userId);
        await services.avatar.CreateOutfit(safeUserSession.userId, request.name, existingAvatar.thumbnailUrl, existingAvatar.headshotUrl, CreateOutfitDetails(safeUserSession.userId, existingAvatar, assets));
    }

    [RequireRobloxSession]
    [HttpPost("/v1/outfits/{outfitId:long}/delete")]
    [HttpPost("/apisite/avatar/v1/outfits/{outfitId:long}/delete")]
    public async Task DeleteOutfit(long outfitId)
    {
        FeatureCheck();
        var info = await services.avatar.GetOutfitById(outfitId);
        if (info.details.userId != safeUserSession.userId)
            throw Forbidden("Forbidden");

        await services.avatar.DeleteOutfit(outfitId);
    }

    [RequireRobloxSession]
    [HttpPost("/v1/outfits/{outfitId:long}/rename")]
    [HttpPost("/apisite/avatar/v1/outfits/{outfitId:long}/rename")]
    public async Task RenameOutfit(long outfitId, [Required, FromBody] UpdateOutfitRequest request)
    {
        FeatureCheck();
        if (request.name == null)
            throw BadRequest("Name field required in body");

        var outfitDetails = await services.avatar.GetOutfitById(outfitId);
        if (outfitDetails.details.userId != safeUserSession.userId)
            throw Forbidden("Forbidden");

        await services.avatar.RenameOutfit(outfitId, request.name);
    }

    [RequireRobloxSession]
    [HttpPatch("/v1/outfits/{outfitId:long}")]
    [HttpPatch("/apisite/avatar/v1/outfits/{outfitId:long}")]
    public async Task UpdateOutfit(long outfitId, [Required, FromBody] UpdateOutfitRequest request)
    {
        FeatureCheck();
        var outfitDetails = await services.avatar.GetOutfitById(outfitId);
        if (outfitDetails.details.userId != safeUserSession.userId)
            throw Forbidden("Forbidden");

        var assets = await services.avatar.GetWornAssets(safeUserSession.userId);
        var existingAvatar = await services.avatar.GetAvatar(safeUserSession.userId);
        await services.avatar.UpdateOutfit(outfitId, request.name, existingAvatar.thumbnailUrl, existingAvatar.headshotUrl, CreateOutfitDetails(safeUserSession.userId, existingAvatar, assets));
    }

    [AllowRobloxAnonymous]
    [HttpGet("/v1/users/{userId:long}/avatar")]
    [HttpGet("/apisite/avatar/v1/users/{userId:long}/avatar")]
    public async Task<dynamic> GetAvatar(long userId)
    {
        var assets = await services.avatar.GetWornAssets(userId);
        var existingAvatar = await services.avatar.GetAvatar(userId);
        var multiGetResults = await services.assets.MultiGetInfoById(assets);

        return new
        {
            existingAvatar.scales,
            playerAvatarType = existingAvatar.avatarType,
            bodyColors = (ColorEntry)existingAvatar,
            assets = multiGetResults.Select(c => new
            {
                id = c.id,
                name = c.name,
                assetType = new
                {
                    id = (int)c.assetType,
                    name = c.assetType,
                },
                currentVersionId = c.id,
            }),
        };
    }

    [AllowRobloxAnonymous]
    [HttpGet("/v1/avatar")]
    [HttpGet("/apisite/avatar/v1/avatar")]
    public async Task<dynamic> GetMyAvatar()
    {
        return await GetAvatar(userSession?.userId ?? (long.TryParse(HttpContext.Request.Headers[RobloxWebContextConstants.UserIdHeaderName], out var userId) ? userId : 1));
    }

    [AllowRobloxAnonymous]
    [HttpGet("/v1/avatar/metadata")]
    [HttpGet("/apisite/avatar/v1/avatar/metadata")]
    public dynamic GetAvatarMetadata()
    {
        return new
        {
            enableDefaultClothingMessage = false,
            isAvatarScaleEmbeddedInTab = true,
            isBodyTypeScaleOutOfTab = true,
            scaleHeightIncrement = 0.05,
            scaleWidthIncrement = 0.05,
            scaleHeadIncrement = 0.05,
            scaleProportionIncrement = 0.05,
            scaleBodyTypeIncrement = 0.05,
            supportProportionAndBodyType = true,
            showDefaultClothingMessageOnPageLoad = false,
            areThreeDeeThumbsEnabled = true,
        };
    }

    [AllowRobloxAnonymous]
    [HttpGet("/v1/avatar-rules")]
    [HttpGet("/apisite/avatar/v1/avatar-rules")]
    public dynamic GetAvatarRules()
    {
        return new
        {
            playerAvatarTypes = Enum.GetNames<AvatarType>(),
            scales = new
            {
                height = new { min = 0.9, max = 1.05, increment = 0.01 },
                width = new { min = 0.7, max = 1.0, increment = 0.01 },
                head = new { min = 0.95, max = 1.0, increment = 0.01 },
                proportion = new { min = 0.0, max = 1.0, increment = 0.01 },
                bodyType = new { min = 0.0, max = 1.0, increment = 0.01 },
            },
            wearableAssetTypes = new List<dynamic>()
            {
                new { maxNumber = 3, id = 8, name = "Hat" },
                new { maxNumber = 1, id = 41, name = "Hair Accessory" },
                new { maxNumber = 1, id = 42, name = "Face Accessory" },
                new { maxNumber = 1, id = 43, name = "Neck Accessory" },
                new { maxNumber = 1, id = 44, name = "Shoulder Accessory" },
                new { maxNumber = 1, id = 45, name = "Front Accessory" },
                new { maxNumber = 1, id = 46, name = "Back Accessory" },
                new { maxNumber = 1, id = 47, name = "Waist Accessory" },
                new { maxNumber = 1, id = 18, name = "Face" },
                new { maxNumber = 1, id = 19, name = "Gear" },
                new { maxNumber = 1, id = 17, name = "Head" },
                new { maxNumber = 1, id = 29, name = "Left Arm" },
                new { maxNumber = 1, id = 30, name = "Left Leg" },
                new { maxNumber = 1, id = 12, name = "Pants" },
                new { maxNumber = 1, id = 28, name = "Right Arm" },
                new { maxNumber = 1, id = 31, name = "Right Leg" },
                new { maxNumber = 1, id = 11, name = "Shirt" },
                new { maxNumber = 1, id = 2, name = "T-Shirt" },
                new { maxNumber = 1, id = 27, name = "Torso" },
                new { maxNumber = 1, id = 48, name = "Climb Animation" },
                new { maxNumber = 1, id = 49, name = "Death Animation" },
                new { maxNumber = 1, id = 50, name = "Fall Animation" },
                new { maxNumber = 1, id = 51, name = "Idle Animation" },
                new { maxNumber = 1, id = 52, name = "Jump Animation" },
                new { maxNumber = 1, id = 53, name = "Run Animation" },
                new { maxNumber = 1, id = 54, name = "Swim Animation" },
                new { maxNumber = 1, id = 55, name = "Walk Animation" },
                new { maxNumber = 1, id = 56, name = "Pose Animation" },
                new { maxNumber = 0, id = 61, name = "Emote Animation" },
            },
            bodyColorsPalette = Roblox.Models.Avatar.AvatarMetadata.GetColors(),
            basicBodyColorsPalette = GetBasicBodyColorsPalette(),
            minimumDeltaEBodyColorDifference = 11.4,
            defaultClothingAssetLists = new
            {
                defaultShirtAssetIds = new List<long>() { 1, 2 },
                defaultPantAssetIds = new List<long>() { 1, 2 },
            },
            bundlesEnabledForUser = false,
            emotesEnabledForUser = false,
        };
    }

    private static OutfitExtendedDetails CreateOutfitDetails(long userId, AvatarWithColors existingAvatar, IEnumerable<long> assets)
    {
        return new OutfitExtendedDetails()
        {
            details = new OutfitAvatar()
            {
                headColorId = existingAvatar.headColorId,
                torsoColorId = existingAvatar.torsoColorId,
                leftArmColorId = existingAvatar.leftArmColorId,
                rightArmColorId = existingAvatar.rightArmColorId,
                leftLegColorId = existingAvatar.leftLegColorId,
                rightLegColorId = existingAvatar.rightLegColorId,
                height = existingAvatar.scales.height,
                width = existingAvatar.scales.width,
                head = existingAvatar.scales.head,
                depth = existingAvatar.scales.depth,
                proportion = existingAvatar.scales.proportion,
                bodyType = existingAvatar.scales.bodyType,
                avatarType = existingAvatar.avatarType,
                userId = userId,
            },
            assetIds = assets,
        };
    }

    private static RobloxException BadRequest(string message)
    {
        return new RobloxException(RobloxException.BadRequest, 0, message);
    }

    private static RobloxException Forbidden(string message)
    {
        return new RobloxException(RobloxException.Forbidden, 0, message);
    }

    private static List<dynamic> GetBasicBodyColorsPalette()
    {
        return new List<dynamic>()
        {
            new { brickColorId = 364, hexColor = "#5A4C42", name = "Dark taupe" },
            new { brickColorId = 217, hexColor = "#7C5C46", name = "Brown" },
            new { brickColorId = 359, hexColor = "#AF9483", name = "Linen" },
            new { brickColorId = 18, hexColor = "#CC8E69", name = "Nougat" },
            new { brickColorId = 125, hexColor = "#EAB892", name = "Light orange" },
            new { brickColorId = 361, hexColor = "#564236", name = "Dirt brown" },
            new { brickColorId = 192, hexColor = "#694028", name = "Reddish brown" },
            new { brickColorId = 351, hexColor = "#BC9B5D", name = "Cork" },
            new { brickColorId = 352, hexColor = "#C7AC78", name = "Burlap" },
            new { brickColorId = 5, hexColor = "#D7C59A", name = "Brick yellow" },
            new { brickColorId = 153, hexColor = "#957977", name = "Sand red" },
            new { brickColorId = 1007, hexColor = "#A34B4B", name = "Dusty Rose" },
            new { brickColorId = 101, hexColor = "#DA867A", name = "Medium red" },
            new { brickColorId = 1025, hexColor = "#FFC9C9", name = "Pastel orange" },
            new { brickColorId = 330, hexColor = "#FF98DC", name = "Carnation pink" },
            new { brickColorId = 135, hexColor = "#74869D", name = "Sand blue" },
            new { brickColorId = 305, hexColor = "#527CAE", name = "Steel blue" },
            new { brickColorId = 11, hexColor = "#80BBDC", name = "Pastel Blue" },
            new { brickColorId = 1026, hexColor = "#B1A7FF", name = "Pastel violet" },
            new { brickColorId = 321, hexColor = "#A75E9B", name = "Lilac" },
            new { brickColorId = 107, hexColor = "#008F9C", name = "Bright bluish green" },
            new { brickColorId = 310, hexColor = "#5B9A4C", name = "Shamrock" },
            new { brickColorId = 317, hexColor = "#7C9C6B", name = "Moss" },
            new { brickColorId = 29, hexColor = "#A1C48C", name = "Medium green" },
            new { brickColorId = 105, hexColor = "#E29B40", name = "Br. yellowish orange" },
            new { brickColorId = 24, hexColor = "#F5CD30", name = "Bright yellow" },
            new { brickColorId = 334, hexColor = "#F8D96D", name = "Daisy orange" },
            new { brickColorId = 199, hexColor = "#635F62", name = "Dark stone grey" },
            new { brickColorId = 1002, hexColor = "#CDCDCD", name = "Mid gray" },
            new { brickColorId = 1001, hexColor = "#F8F8F8", name = "Institutional white" },
        };
    }
}
