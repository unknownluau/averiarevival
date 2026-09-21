using Microsoft.AspNetCore.Mvc;
using Roblox.Dto.Economy;
using Roblox.Dto.Games;
using Roblox.Exceptions;
using Roblox.Models;
using Roblox.Models.Assets;
using Roblox.Models.Db;
using Roblox.Services.Exceptions;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using Roblox.Services.App.FeatureFlags;
using Roblox.Web.Infrastructure.Metadata;

namespace Roblox.Website.Controllers;

[ApiController]
[Route("/apisite/badges/v1/")]
public class BadgesControllerV1 : ControllerBase
{
    // base: https://apidocs.sixteensrc.zip/badges/docs.html#/
    
    // Gets badge information by the badge id.
    [RequireRobloxSession]
    [HttpGet("badges/{badgeId:long}")]
    [HttpGetBypass("/v1/badges/{badgeId:long}")]
    [HttpPost("badges/{badgeId:long}")]
    [HttpPostBypass("/v1/badges/{badgeId:long}")]
    public async Task<BadgeAssetDetails> GetBadgeDetails(long badgeId) 
    {
        FeatureFlags.FeatureCheck(FeatureFlag.BadgesEnabled);
        // TODO: is this even needed?
        var basicBadgeInfo = await services.badges.GetBadgeInfo(badgeId);
        if (basicBadgeInfo is null) {
            throw new BadRequestException(0, "Badge is invalid or does not exist");
        }

        var uni = await services.games.GetUniverseInfo(basicBadgeInfo.universeId);
        // no need to check if it's null right?
        var badgeInfo = await services.badges.GetBadgeInfoExtended(badgeId, uni, 1, 0, null);
        
        return badgeInfo.First();
    }
    
    // Updates badge configuration.
    [RequireRobloxSession]
    [HttpPatch("badges/{badgeId:long}")]
    public async Task<dynamic> UpdateBadgeConfig(long badgeId, [Required, FromBody] BadgeUpdateRequest request) 
    {
        FeatureFlags.FeatureCheck(FeatureFlag.BadgesEnabled);

        await services.assets.ValidatePermissions(badgeId, safeUserSession.userId);
        
        var basicBadgeInfo = await services.badges.GetBadgeInfo(badgeId);
        if (basicBadgeInfo is null) 
            throw new BadRequestException(0, "Badge is invalid or does not exist");
        

        await services.assets.EnsureAssetIsModerated(badgeId);
        await services.badges.UpdateBadge(badgeId, request.enabled);
        await services.assets.UpdateAsset(badgeId);
        return new { };
    }
    
    // Gets badge by their awarding game.
    [RequireRobloxSession]
    [HttpGet("universes/{universeId:long}/badges")]
    [HttpGetBypass("/v1/universes/{universeId:long}/badges")]
    public async Task<RobloxCollectionPaginated<BadgeAssetDetails>> GetUniverseBadges(long universeId, int limit, string? cursor, SortOrder? sortOrder)
    {        
        FeatureFlags.FeatureCheck(FeatureFlag.BadgesEnabled);
        if (!await services.cooldown.TryIncrementBucketCooldown("Badges:V1:Universes:Ip:" + GetIP(), 60, TimeSpan.FromMinutes(1)) ||
            !await services.cooldown.TryIncrementBucketCooldown("Badges:V1:Universes:Id:" + safeUserSession.userId, 80, TimeSpan.FromMinutes(1)) ||
            !await services.cooldown.TryIncrementBucketCooldown("Badges:V1:Universes:UniverseId:" + universeId, 100, TimeSpan.FromMinutes(1)))
            throw new RobloxException(RobloxException.TooManyRequests);
        
        if (limit is > 100 or < 1) limit = 10;
        var offset = cursor != null ? int.Parse(cursor) : 0;
        var uni = await services.games.GetUniverseInfo(universeId);
        var badgeInfo = (await services.badges.GetBadgesForUniverse(uni, limit, offset, sortOrder)).ToList();
        
        return new RobloxCollectionPaginated<BadgeAssetDetails>()
        {
            previousPageCursor = offset >= limit ? (offset - limit).ToString() : null,
            nextPageCursor = badgeInfo.Count() >= limit ? (offset + limit).ToString() : null,
            data = badgeInfo,
        };
    }
    
    // Gets a list of badges a user has been awarded.
    [RequireRobloxSession]
    [HttpGet("users/{userId:long}/badges")]
    [HttpGetBypass("/v1/users/{userId:long}/badges")]
    public async Task<RobloxCollectionPaginated<BadgeAssetDetails>> GetBadges(long userId, int limit, string? cursor, SortOrder? sortOrder)
    {
        FeatureFlags.FeatureCheck(FeatureFlag.BadgesEnabled);
        if (limit is > 100 or < 1) limit = 10;
        var offset = cursor != null ? int.Parse(cursor) : 0;
        var badgeInfo = (await services.badges.GetBadgesForUser(userId, limit, offset, sortOrder)).ToList();
        
        return new RobloxCollectionPaginated<BadgeAssetDetails>()
        {
            previousPageCursor = offset >= limit ? (offset - limit).ToString() : null,
            nextPageCursor = badgeInfo.Count() >= limit ? (offset + limit).ToString() : null,
            data = badgeInfo,
        };
    }
    
    // Gets timestamps for when badges were awarded to a user.
    [RequireRobloxSession]
    [HttpGet("users/{userId:long}/badges/awarded-dates")]
    [HttpGetBypass("/v1/users/{userId:long}/badges/awarded-dates")]
    public async Task<dynamic> GetBadgeTimestamps(long userId, string badgeIds)
    {
        FeatureFlags.FeatureCheck(FeatureFlag.BadgesEnabled);
        var ids = badgeIds.Split(",").Select(long.Parse).ToArray();
        if (!ids.Any())
            return Array.Empty<BadgeAwardDate>();
        return new
        {
            data = await services.badges.GetUserBadgeAwardedDates(userId, ids),
        };
    }

    private Dictionary<long, long> badgeRewards = new Dictionary<long, long>
    {
            { 558057, 531032 }, // KET Egg
            { 558048, 531033 }, // Egg of Tick Tock
            { 558056, 531034 }, // Eggmin
            { 558052, 531037 }, // Chickegg
            { 558049, 531041 }, // TIX Egg
            { 557815, 531042 }, // Knight Egg
            { 557823, 531043 }, // Yolkist
            { 557917, 531046 }, // Basic Egg
            { 558080, 531056 }, // Sorcus Egg
            { 557831, 555367 }, // Bellegg
            { 557848, 555369 }, // Egg of Friendship
            { 557860, 555370 }, // Egg of the Hill
            { 557868, 555371 }, // Eggfection
            { 557874, 555380 }, // Doggo Egg
            { 557881, 555389 }, // Builderman Egg
            { 557883, 555393 }, // Royal Faberg� Egg
            { 557893, 555619 }, // Hipster Egg of Retro
            { 557896, 555620 }, // Top of the World Egg
            { 557903, 555621 }, // Inkwell Egg
            { 557908, 555623 }, // The Eggtopus
            { 557914, 555626 }, // Seal Egg
            { 557921, 555631 }, // Billy The Egg
            { 557927, 555644 }, // Eggcano
            { 557935, 555646 }, // The Amber Egg
            { 557951, 557460 }, // The Obsidian Egg
            { 557961, 557463 }, // Pompeiian Egg
            { 557966, 557466 }, // Mad Scientist Egg
            { 557973, 557467 }, // Molten Meteoric Core Egg
            { 557989, 557469 }, // Egg of the Phoenix
            { 557993, 557471 }, // Eggmageddon
            { 558006, 557474 }, // Preggstoric Fossil
            { 558015, 557481 }, // Egg of Luck
            { 558023, 557493 }, // Egg of Life
            { 558033, 557502 }, // S.S. Egg - The Mighty Dirigible
            { 558038, 557515 }, // Eggsplorer
            { 558039, 557523 }, // Black Iron Faberg� Egg
            { 558018, 558798 }, // Arborist's Verdant Egg of Leafyness
            { 558041, 558793 }, // Insanely Valuable Crystal Egg
            { 558062, 562740 }, // The Final Faberg�gg
            { 558010, 562746 }, // The Pirate Egg
    };
    
    // Award a badge to a user.
    [RequireRccRequest]
    [HttpPost("users/{userId:long}/badges/{badgeId:long}/award-badge")]
    [HttpPostBypass("/v1/users/{userId:long}/badges/{badgeId:long}/award-badge")]
    [HttpPostBypass("/assets/award-badge")]
    public async Task<dynamic> AwardBadge(long userId, long badgeId, long? placeId)
    {
        FeatureFlags.FeatureCheck(FeatureFlag.BadgesEnabled);
        if (!isRCC) {
            throw new PermissionException(badgeId, safeUserSession.userId);
        }

        if (placeId is null) 
        {
            var robloxPlaceId = Request.Headers["Roblox-Place-Id"].ToString();
            if (!long.TryParse(robloxPlaceId, out _)) 
            {
                throw new BadRequestException(0, "Missing Roblox-Place-Id Header");
            }
            placeId = long.Parse(robloxPlaceId);
        }

        if (userId is 0) {
            // attempt to pull from query, in accordance to assets/award-badge
            if (string.IsNullOrEmpty(Request.Query["userId"].ToString()) || !long.TryParse(Request.Query["userId"], out _))
                throw new BadRequestException(0, "User does not exist.");
            userId = long.Parse(Request.Query["userId"]!);
        }
        
        if (badgeId is 0) {
            // attempt to pull from query, in accordance to assets/award-badge
            if (string.IsNullOrEmpty(Request.Query["badgeId"].ToString()) || !long.TryParse(Request.Query["badgeId"], out _))
                throw new BadRequestException(0, "Badge does not exist.");
            badgeId = long.Parse(Request.Query["badgeId"]!);
        }

        // checks if userId is an actual user
        var user = await services.users.GetUserById(userId);
        var universeId = await services.games.GetUniverseId(placeId.Value);
        // shouldnt have to check null cuz of above right?
        var uni = await services.games.GetUniverseInfo(universeId);
        var badgeInfo = await services.badges.GetBadgeInfo(badgeId);

        if (badgeInfo is null) 
            throw new BadRequestException(0, "Badge is invalid or does not exist");
        
        if (!badgeInfo.enabled)
            throw new BadRequestException(8, "The badge is disabled.");

        if (badgeInfo.universeId != universeId)
            throw new ForbiddenException(8, "The place doesn't have permission to award the badge.");

        if ((await services.users.GetUserAssets(userId, badgeId)).Any())
            throw new BadRequestException(0, "User already owns the badge");
        // TODO: put proper error code here from apidocs sixteensrc 
        await services.users.CreateUserAsset(userId, badgeInfo.assetId);
        await services.assets.IncrementSaleCount(badgeId);


        if (badgeRewards.TryGetValue(badgeId, out var hatId))
        {
            await services.users.CreateUserAsset(userId, hatId);
        }

        if (Request.Path == "/assets/award-badge") 
        {
            var badgeProd = await services.assets.GetAssetCatalogInfo(badgeId);
            return $"{user.username} won {badgeProd.creatorName}'s {badgeProd.name} award!";
        }

        
        return new 
        {
            creatorType = uni.creator.type,
            creatorId = uni.creator.id,
            awardAssetIds = Array.Empty<dynamic>()
        };
    }
    
    // Removes a badge from a user.
    [RequireRccRequest]
    [HttpDelete("users/{userId:long}/badges/{badgeId:long}")]
    public async Task<dynamic> RemoveBadgeFromUser(long userId, long badgeId)
    {
        FeatureFlags.FeatureCheck(FeatureFlag.BadgesEnabled);
        if (!isRCC) {
            throw new PermissionException(badgeId, safeUserSession.userId);
        }
        
        // checks if userId is an actual user
        await services.users.GetUserById(userId);
        var badgeInfo = await services.badges.GetBadgeInfo(badgeId);
        if (badgeInfo is null) {
            throw new BadRequestException(0, "Badge is invalid or does not exist");
        }
        
        // TODO: check if this is necessary
        // might be necessary?
        // if ((await services.users.GetUserAssets(userId, badgeId)).Any()) {
        //     await services.users.DeleteUserAsset(userId, badgeId);
        // }
        await services.users.DeleteUserAsset(userId, badgeId);
        
        return new {};
    }
    
    // Removes a badge from the authenticated user.
    // [HttpDelete("users/badges/{badgeId:long}")]
    // public async Task<dynamic> RemoveBadgeFromSelf(long badgeId)
    // {
        // TODO: is this safe?
        // var userId = safeUserSession.userId;
        //
        // var badgeInfo = await services.badges.GetBadgeInfo(badgeId);
        // if (badgeInfo is null) {
        //     throw new BadRequestException(0, "Badge is invalid or does not exist");
        // }
        //
        // await services.users.DeleteUserAsset(userId, badgeId);
        
    //     return new {};
    // }
}