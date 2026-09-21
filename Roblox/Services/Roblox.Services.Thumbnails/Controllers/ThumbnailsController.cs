using Microsoft.AspNetCore.Mvc;
using Roblox.Dto.Thumbnails;
using Roblox.Models;
using Roblox.Models.Thumbnails;
using Roblox.Services.Exceptions;
using Roblox.Web.Infrastructure.Controllers;
using Roblox.Web.Infrastructure.Metadata;

namespace Roblox.Services.Thumbnails.Controllers;

[ApiController]
[Route("/")]
public class ThumbnailsController : RobloxControllerBase
{
    [AllowRobloxAnonymous]
    [HttpGet("/v1/users/avatar-headshot")]
    [HttpGet("/apisite/thumbnails/v1/users/avatar-headshot")]
    public async Task<RobloxCollection<ThumbnailEntry>> GetUserHeadshots([FromQuery] string userIds)
    {
        var parsed = ParseIdList(userIds);
        var result = (await services.thumbnails.GetUserHeadshots(parsed)).ToList();
        return new()
        {
            data = result,
        };
    }

    [AllowRobloxAnonymous]
    [HttpGet("/v1/users/avatar")]
    [HttpGet("/apisite/thumbnails/v1/users/avatar")]
    public async Task<RobloxCollection<ThumbnailEntry>> GetUserThumbnails([FromQuery] string userIds)
    {
        var parsed = ParseIdList(userIds);
        var result = await services.thumbnails.GetUserThumbnails(parsed);
        return new()
        {
            data = result,
        };
    }

    [AllowRobloxAnonymous]
    [HttpGet("/v1/users/avatar-3d")]
    [HttpGet("/apisite/thumbnails/v1/users/avatar-3d")]
    public async Task<RobloxCollection<ThumbnailEntry>> GetUserThumbnails3D([FromQuery] string userIds)
    {
        var parsed = ParseIdList(userIds);
        var result = (await services.thumbnails.GetUserThumbnails3D(parsed)).ToList();
        return new()
        {
            data = result,
        };
    }

    [AllowRobloxAnonymous]
    [HttpGet("/v1/assets")]
    [HttpGet("/apisite/thumbnails/v1/assets")]
    public async Task<RobloxCollection<ThumbnailEntry>> GetAssetThumbnails([FromQuery] string assetIds)
    {
        var parsed = ParseIdList(assetIds);
        var result = await services.thumbnails.GetAssetThumbnails(parsed);
        return new()
        {
            data = result,
        };
    }

    [AllowRobloxAnonymous]
    [HttpGet("/v1/users/outfits")]
    [HttpGet("/apisite/thumbnails/v1/users/outfits")]
    public async Task<RobloxCollection<ThumbnailEntry>> GetUserOutfitThumbnails([FromQuery] string userOutfitIds)
    {
        var parsed = ParseIdList(userOutfitIds);
        var result = await services.thumbnails.GetUserOutfitThumbnails(parsed);
        return new()
        {
            data = result,
        };
    }

    [AllowRobloxAnonymous]
    [HttpGet("/v1/groups/icons")]
    [HttpGet("/apisite/thumbnails/v1/groups/icons")]
    public async Task<RobloxCollection<ThumbnailEntry>> GetGroupIcons([FromQuery] string groupIds)
    {
        var parsed = ParseIdList(groupIds);
        var result = await services.thumbnails.GetGroupIcons(parsed);
        return new()
        {
            data = result,
        };
    }

    [AllowRobloxAnonymous]
    [HttpGet("/v1/games/icons")]
    [HttpGet("/apisite/thumbnails/v1/games/icons")]
    public async Task<RobloxCollection<ThumbnailEntry>> GetUniverseIcons([FromQuery] string universeIds)
    {
        var parsed = ParseIdList(universeIds);
        var result = await services.thumbnails.GetUniverseIcons(parsed);
        return new()
        {
            data = result.Where(c => c.imageUrl != null).Select(c => new ThumbnailEntry
            {
                targetId = c.targetId,
                imageUrl = c.imageUrl,
                state = c.state,
                version = c.version,
            }).ToList(),
        };
    }

    [AllowRobloxAnonymous]
    [HttpGet("/v1/places/gameicons")]
    [HttpGet("/apisite/thumbnails/v1/places/gameicons")]
    public async Task<RobloxCollection<ThumbnailEntry>> GetPlaceIcons([FromQuery] string placeIds)
    {
        var parsed = ParseIdList(placeIds);
        var result = await services.thumbnails.GetPlaceIcons(parsed);
        return new()
        {
            data = result.Where(c => c.imageUrl != null).Select(c => new ThumbnailEntry
            {
                targetId = c.targetId,
                imageUrl = c.imageUrl,
                state = c.state,
                version = c.version,
            }).ToList(),
        };
    }

    [AllowRobloxAnonymous]
    [HttpPost("/v1/batch")]
    [HttpPost("/apisite/thumbnails/v1/batch")]
    public async Task<RobloxCollection<dynamic>> BatchThumbnailsRequest([FromBody] IEnumerable<BatchRequestEntry>? request)
    {
        if (!await services.cooldown.TryIncrementBucketCooldown("Thumbnails:V1:Batch:Ip:" + GetIP(), 60, TimeSpan.FromSeconds(1)))
            throw new RobloxException(RobloxException.TooManyRequests);
        
        var thumbs = request is not null ? request.ToList() : [];
        if(thumbs.Count > 100) throw new RobloxException(RobloxException.BadRequest, 1, "There are too many requested Ids.");
        var allResults = await Task.WhenAll(new List<Task<IEnumerable<dynamic>>>()
        {
            MultiGetThumbnailsGeneric(thumbs, "Avatar", services.thumbnails.GetUserThumbnails),
            MultiGetThumbnailsGeneric(thumbs, "AvatarThumbnail", services.thumbnails.GetUserThumbnails),
            MultiGetThumbnailsGeneric(thumbs, "AvatarHeadShot", services.thumbnails.GetUserHeadshots),
            MultiGetThumbnailsGeneric(thumbs, "AvatarBust", services.thumbnails.GetUserHeadshots),
            MultiGetThumbnailsGeneric(thumbs, "GameIcon", services.thumbnails.GetUniverseIcons),
            MultiGetThumbnailsGeneric(thumbs, "AutoGeneratedGameIcon", services.thumbnails.GetUniverseIcons),
            MultiGetThumbnailsGeneric(thumbs, "ForceAutoGeneratedGameIcon", services.thumbnails.GetUniverseIcons),
            MultiGetThumbnailsGeneric(thumbs, "GameThumbnail", services.thumbnails.GetAssetThumbnails),
            MultiGetThumbnailsGeneric(thumbs, "Asset", services.thumbnails.GetAssetThumbnails),
            MultiGetThumbnailsGeneric(thumbs, "AssetThumbnail", services.thumbnails.GetAssetThumbnails),
            MultiGetThumbnailsGeneric(thumbs, "AutoGeneratedAsset", services.thumbnails.GetPlaceIcons),
            MultiGetThumbnailsGeneric(thumbs, "GroupIcon", services.thumbnails.GetGroupIcons),
        });
        return new RobloxCollection<dynamic>()
        {
            data = allResults.SelectMany(x => x),
        };
    }

    public static async Task<IEnumerable<dynamic>> MultiGetThumbnailsGeneric(List<BatchRequestEntry> thumbs, string type, Func<IEnumerable<long>, Task<IEnumerable<ThumbnailEntry>>> method)
    {
        var idList = thumbs.Where(c => c.type == type).Select(c => c.targetId).ToList();
        if (idList.Count == 0) return Array.Empty<dynamic>();

        var thumbnails = await method(idList);
        var results = new List<dynamic>();

        foreach (var c in thumbnails)
        {
            if (!string.IsNullOrEmpty(c.imageUrl))
            {
                results.Add(new
                {
                    requestId = thumbs.Find(v => v.targetId == c.targetId && v.type == type)?.requestId ?? string.Empty,
                    errorCode = 0,
                    errorMessage = string.Empty,
                    targetId = c.targetId,
                    state = c.state,
                    imageUrl = c.imageUrl,
                    version = c.version
                });
            }
            else
            {
                results.Add(new
                {
                    requestId = thumbs.Find(v => v.targetId == c.targetId && v.type == type)?.requestId ?? string.Empty,
                    errorCode = 4,
                    errorMessage = "The requested Ids are invalid, of an invalid type or missing.",
                    targetId = c.targetId,
                    state = ThumbnailState.Error,
                    imageUrl = (string?)null,
                    version = (string?)null
                });
            }
        }

        return results;
    }

    private static List<long> ParseIdList(string? rawIds)
    {
        if (string.IsNullOrWhiteSpace(rawIds))
        {
            throw CreateBadRequest();
        }

        var parsed = new List<long>();
        foreach (var rawId in rawIds.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            if (!long.TryParse(rawId, out var id))
            {
                throw CreateBadRequest();
            }

            parsed.Add(id);
        }

        parsed = parsed.Distinct().ToList();
        if (parsed.Count is 0 or > 200)
        {
            throw CreateBadRequest();
        }

        return parsed;
    }

    private static RobloxException CreateBadRequest()
    {
        return new RobloxException(RobloxException.BadRequest, 0, "BadRequest");
    }
}
