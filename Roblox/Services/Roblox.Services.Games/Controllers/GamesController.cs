using System.ComponentModel.DataAnnotations;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Mvc;
using Roblox.Dto.Games;
using Roblox.Logging;
using Roblox.Models;
using Roblox.Models.Assets;
using Roblox.Models.Db;
using Roblox.Services.App.FeatureFlags;
using Roblox.Services.Exceptions;
using Roblox.Web.Infrastructure.Controllers;
using Roblox.Web.Infrastructure.Metadata;

namespace Roblox.Services.Games.Controllers;

[ApiController]
[InternalServiceOnly]
[RequireRobloxCsrf]
[RequireRobloxSession]
[Route("/")]
public class GamesController : RobloxControllerBase
{
    [HttpGet("/v1/games")]
    [HttpGet("/apisite/games/v1/games")]
    public async Task<dynamic> MultiGetUniverseInfo(string universeIds)
    {
        FeatureFlags.FeatureCheck(FeatureFlag.GamesEnabled);
        var sp = universeIds.Split(",").Select(long.Parse);
        var result = await services.games.MultiGetUniverseInfo(sp);
        return new
        {
            data = result,
        };
    }

    [HttpGet("/v1/games/sorts")]
    [HttpGet("/apisite/games/v1/games/sorts")]
    public async Task<dynamic> GetGameSorts(string? gameSortsContext)
    {
        FeatureFlags.FeatureCheck(FeatureFlag.GamesEnabled);
        var sorts = new Dictionary<string, dynamic>()
        {
            {
                "popular", new
                {
                    token = "popular",
                    name = "Popular",
                    displayName = "Popular",
                    gameSetTypeId = 1,
                    gameSetTargetId = 90
                }
            },
            {
                "classics", new
                {
                    token = "classics",
                    name = "Classics",
                    displayName = "Classics",
                    gameSetTypeId = 2,
                    gameSetTargetId = 91
                }
            },
            {
                "recent", new
                {
                    token = "recent",
                    name = "Recent",
                    displayName = "Recent",
                    gameSetTypeId = 3,
                    gameSetTargetId = 92
                }
            },
            {
                "mostFavorited", new
                {
                    token = "mostFavorited",
                    name = "Most Favorited",
                    displayName = "Most Favorited",
                    gameSetTypeId = 4,
                    gameSetTargetId = 93
                }
            },
            // {
            //     "recentlyUpdated", new
            //     {
            //         token = "recentlyUpdated",
            //         name = "Recently Updated",
            //         displayName = "Recently Updated",
            //     }
            // },
            // {
            //     "recentlyCreated", new
            //     {
            //         token = "recentlyCreated",
            //         name = "Recently Created",
            //         displayName = "Recently Created",
            //     }
            // },
        };

        var results = new List<dynamic>();
        if (gameSortsContext != null && gameSortsContext is "HomeSorts" or "UnifiedHomeSorts")
        {
            // we need to check if player actually has anything recent before showing recent sort
            var recent = await services.games.GetRecentGames(safeUserSession.userId, 1);
            if (recent.Any())
            {
                results.Add(sorts["recent"]);
                //results.Add(sorts["roulette"]);
                results.Add(sorts["popular"]);
                // results.Add(sorts["mostFavorited"]);
            }
        }
        else
        {
            results.Add(sorts["popular"]);
            results.Add(sorts["classics"]);
            //results.Add(sorts["roulette"]);
            results.Add(sorts["mostFavorited"]);
            //results.Add(sorts["recentlyUpdated"]);
            // results.Add(sorts["recentlyCreated"]);
        }

        return new
        {
            sorts = results.Select(c => new
            {
                c.token,
                c.name,
                c.displayName,
                c.gameSetTypeId,
                c.gameSetTargetId,
                timeOptionsAvailable = false,
                genreOptionsAvailable = false,
                numberOfRows = 1,
                numberOfGames = 0,
                isDefaultSort = true,
                contextUniverseId = (long?)null,
                contextCountryRegionId = (int?)null,
                tokenExpiryInSeconds = 86400,
            }),
            timeFilters = new[]
            {
                new { token = "Now", name = "Now", tokenExpiryInSeconds = 3600 },
                new { token = "PastDay", name = "PastDay", tokenExpiryInSeconds = 3600 },
                new { token = "PastWeek", name = "PastWeek", tokenExpiryInSeconds = 3600 },
                new { token = "PastMonth", name = "PastMonth", tokenExpiryInSeconds = 3600 },
                new { token = "AllTime", name = "AllTime", tokenExpiryInSeconds = 3600 }
            },
            genreFilters = new[]
            {
                new { token = "T638364961735517991_1_89de", name = "All", tokenExpiryInSeconds = 3600 },
                new { token = "T638364961735518009_19_3d2", name = "Building", tokenExpiryInSeconds = 3600 },
                new { token = "T638364961735518045_11_3de6", name = "Horror", tokenExpiryInSeconds = 3600 },
                new { token = "T638364961735518062_7_558c", name = "Town and City", tokenExpiryInSeconds = 3600 },
                new { token = "T638364961735518076_17_c371", name = "Military", tokenExpiryInSeconds = 3600 },
                new { token = "T638364961735518094_15_2056", name = "Comedy", tokenExpiryInSeconds = 3600 },
                new { token = "T638364961735518107_8_6d4f", name = "Medieval", tokenExpiryInSeconds = 3600 },
                new { token = "T638364961735518120_13_c168", name = "Adventure", tokenExpiryInSeconds = 3600 },
                new { token = "T638364961735518134_9_e6aa", name = "Sci-Fi", tokenExpiryInSeconds = 3600 },
                new { token = "T638364961735518156_12_13fb", name = "Naval", tokenExpiryInSeconds = 3600 },
                new { token = "T638364961735518169_20_46a", name = "FPS", tokenExpiryInSeconds = 3600 },
                new { token = "T638364961735518183_21_4bbf", name = "RPG", tokenExpiryInSeconds = 3600 },
                new { token = "T638364961735518192_14_efc6", name = "Sports", tokenExpiryInSeconds = 3600 },
                new { token = "T638364961735518205_10_fa83", name = "Fighting", tokenExpiryInSeconds = 3600 },
                new { token = "T638364961735518223_16_5d38", name = "Western", tokenExpiryInSeconds = 3600 }
            },
            gameFilters = new[]
            {
                new { token = "T638364961735518263_Any_56d2", name = "Any", tokenExpiryInSeconds = 3600 },
                new { token = "T638364961735518277_Classic_a1f4", name = "Classic", tokenExpiryInSeconds = 3600 }
            },
            pageContext = new
            {
                pageId = "f5b1510e-3810-42ab-8135-8ffa5ef221ba",
                isSeeAllPage = (bool?)null
            },
            gameSortStyle = (string?)null
        };
    }

    [HttpGet("/v1/games/list")]
    [HttpGet("/apisite/games/v1/games/list")]
    public async Task<dynamic> GetGamesList(string? sortToken, int maxRows = 10, Genre? genre = null, string? keyword = null)
    {
        FeatureFlags.FeatureCheck(FeatureFlag.GamesEnabled);
        if (!await services.cooldown.TryIncrementBucketCooldown($"Games:V1:{GetIP()}", 80, TimeSpan.FromMinutes(1)))
             throw new RobloxException(RobloxException.TooManyRequests, 0, "Too many attempts. Try again in a few seconds.");
        
        if (maxRows is > 50 or < 1) maxRows = 50;
        var result = await services.games.GetGamesList(userSession?.userId, sortToken, maxRows, genre, keyword);
        return new
        {
            games = result,
        };
    }

    private static Regex numberRegex { get; } = new("([0-9]+)");

    [HttpGet("/v1/games/multiget-playability-status")]
    [HttpGet("/apisite/games/v1/games/multiget-playability-status")]
    public dynamic MultiGetPlayabilityStatus()
    {
        FeatureFlags.FeatureCheck(FeatureFlag.GamesEnabled);
        var ids = HttpContext.Request.QueryString.Value ?? "";
        return numberRegex.Matches(ids).Select(c => long.Parse(c.Value)).Distinct().Select(c => new
        {
            playabilityStatus = "Playable",
            isPlayable = true,
            universeId = c,
        });
    }

    [HttpGet("/v1/games/{universeId:long}/social-links/list")]
    [HttpGet("/apisite/games/v1/games/{universeId:long}/social-links/list")]
    public dynamic GetSocialLinks()
    {
        FeatureFlags.FeatureCheck(FeatureFlag.GamesEnabled);
        return new
        {
            data = new List<int>(),
        };
    }

    [HttpGet("/v1/games/recommendations/game/{universeId:long}")]
    [HttpGet("/apisite/games/v1/games/recommendations/game/{universeId:long}")]
    public async Task<dynamic> GetRecommendedGames(long universeId, int maxRows = 6)
    {
        FeatureFlags.FeatureCheck(FeatureFlag.GamesEnabled);
        if (!await services.cooldown.TryIncrementBucketCooldown("Games:V1:Recommendations:Ip:" + GetIP(), 60, TimeSpan.FromMinutes(1)) ||
            !await services.cooldown.TryIncrementBucketCooldown("Games:V1:Recommendations:Id:" + safeUserSession.userId, 80, TimeSpan.FromMinutes(1)) ||
            !await services.cooldown.TryIncrementBucketCooldown("Games:V1:Recommendations:UniverseId:" + universeId, 100, TimeSpan.FromMinutes(1)))
            throw new RobloxException(RobloxException.TooManyRequests);
        
        if (maxRows is > 50 or < 1) maxRows = 50;
        // todo: actually add recommendeds
        var result = await services.games.GetGamesList(safeUserSession.userId, "popular", maxRows, null, null);
        return new
        {
            games = result,
        };
    }

    [HttpGet("/v1/games/multiget-place-details")]
    [HttpGet("/apisite/games/v1/games/multiget-place-details")]
    public async Task<IEnumerable<PlaceEntry>> MultiGetPlaceDetails(string placeIds)
    {
        FeatureFlags.FeatureCheck(FeatureFlag.GamesEnabled);
        return await services.games.MultiGetPlaceDetails(placeIds.Split(",").Select(long.Parse));
    }

    [HttpGet("/v1/games/votes")]
    [HttpGet("/apisite/games/v1/games/votes")]
    public async Task<dynamic> GetGameVotes(string universeIds)
    {
        FeatureFlags.FeatureCheck(FeatureFlag.GamesEnabled);
        if (!await services.cooldown.TryIncrementBucketCooldown("Games:V1:Votes:Ip:" + GetIP(), 60, TimeSpan.FromMinutes(1)) ||
            !await services.cooldown.TryIncrementBucketCooldown("Games:V1:Votes:Id:" + safeUserSession.userId, 80, TimeSpan.FromMinutes(1)) ||
            !await services.cooldown.TryIncrementBucketCooldown("Games:V1:Votes:Universes:" + universeIds, 100, TimeSpan.FromMinutes(1)))
            throw new RobloxException(RobloxException.TooManyRequests);
        
        var ids = universeIds.Split(",").Select(long.Parse).Distinct().ToList();
        if (ids.Count is < 1 or > 100)
            throw new RobloxException(400, 0, "BadRequest");
        var uni = await services.games.MultiGetUniverseInfo(ids);

        var result = new List<dynamic>();
        foreach (var item in uni)
        {
            var votes = await services.assets.GetVoteForAsset(item.rootPlaceId);
            result.Add(new
            {
                id = item.id,
                upVotes = votes.upVotes,
                downVotes = votes.downVotes,
            });
        }

        return new
        {
            data = result,
        };
    }

    [HttpPatch("/v1/games/{universeId:long}/user-votes")]
    [HttpPatch("/apisite/games/v1/games/{universeId:long}/user-votes")]
    public async Task VoteOnUniverse(long universeId, [Required, FromBody] VoteRequest request)
    {
        FeatureFlags.FeatureCheck(FeatureFlag.GamesEnabled);
        var uni = await services.games.GetUniverseInfo(universeId);
        await services.assets.VoteOnAsset(uni.rootPlaceId, safeUserSession.userId, request.vote);
    }

    [HttpGet("/v1/users/{userId:long}/count")]
    [HttpGet("/apisite/games/v1/users/{userId:long}/count")]
    public async Task<dynamic> GetUserGameCount(long userId)
    {
        FeatureFlags.FeatureCheck(FeatureFlag.GamesEnabled);
        var localUserId = safeUserSession.userId;
        await using var placeCountLock =
            await Services.Cache.redLock.CreateLockAsync("GetPlaceCountV1:UserId:" + localUserId,
                TimeSpan.FromSeconds(3));
        if (!placeCountLock.IsAcquired)
        {
            Writer.Info(LogGroup.AbuseDetection, "GetPlaceCount API could not acquire placeCuntLock");
            throw new RobloxException(RobloxException.TooManyRequests, 0, "Too many attempts. Try again in a few seconds.");
        }

        var uniCount = await services.games.GetUserPlaceCount(userId);
        return new
        {
            universeCount = uniCount,
        };
    }

    [HttpGet("/v1/games/{universeId:long}/game-passes")]
    [HttpGet("/apisite/games/v1/games/{universeId:long}/game-passes")]
    public async Task<RobloxCollectionPaginated<UniverseGamePassEntry>> GetUniverseGamePasses(long universeId, long? unfiltered = 0, SortOrder? sortOrder = SortOrder.Asc, int? limit = 10, string? cursor = null)
    {
        FeatureFlags.FeatureCheck(FeatureFlag.GamesEnabled);
        if (limit is > 100 or < 1) limit = 10;
        int offset = int.Parse(cursor ?? "0");
        var result = (await services.games.GetGamePassesForUniverse(universeId, limit ?? 10, offset, userSession?.userId, sortOrder ?? SortOrder.Asc)).ToList();

        return new RobloxCollectionPaginated<UniverseGamePassEntry>()
        {
            nextPageCursor = result.Count >= limit ? (offset + limit).ToString() : null,
            previousPageCursor = offset >= limit ? (offset - limit).ToString() : null,
            data = result,
        };
    }

    [HttpGet("/v1/games/game-passes/{assetId:long}")]
    [HttpGet("/apisite/games/v1/games/game-passes/{assetId:long}")]
    public async Task<GamePassDetails> GetGamePassInfo(long assetId)
    {
        FeatureFlags.FeatureCheck(FeatureFlag.GamesEnabled);
        return await services.games.GetGamePassInfo(assetId);
    }
    
    [HttpGet("/v1/games/{universeId:long}/favorites")]
    public async Task<dynamic> GetFavoriteStatus(long universeId)
    {
        return new
        {
            isFavorited = await services.assets.GetFavoriteStatus(safeUserSession.userId, universeId)
        };
    }
    
    //v2
    [HttpGet("/v2/users/{userId:long}/games")]
    [HttpGet("/apisite/games/v2/users/{userId:long}/games")]
    public async Task<RobloxCollectionPaginated<GamesForCreatorEntry>> GetUserGames(long userId,
        string? sortOrder, string? accessFilter, int limit, string? cursor = null)
    {
        FeatureFlags.FeatureCheck(FeatureFlag.GamesEnabled);
        
        if (limit is > 100 or < 1) limit = 10;
        int offset = int.Parse(cursor ?? "0");
        var result =
            (await services.games.GetGamesForType(CreatorType.User, userId, limit, offset, sortOrder ?? "asc", accessFilter ?? "All")).ToList();
        return new RobloxCollectionPaginated<GamesForCreatorEntry>()
        {
            nextPageCursor = result.Count >= limit ? (offset+limit).ToString(): null,
            previousPageCursor = offset >= limit ? (offset-limit).ToString() : null,
            data = result,
        };
    }
    
    [HttpGet("/v2/groups/{groupId:long}/games")]
    [HttpGet("/apisite/games/v2/groups/{groupId:long}/games")]
    public async Task<RobloxCollectionPaginated<GamesForCreatorEntry>> GetGroupGames(long groupId,
        string? sortOrder, string? accessFilter, int limit, string? cursor = null)
    {
        FeatureFlags.FeatureCheck(FeatureFlag.GamesEnabled);
        
        if (limit is > 100 or < 1) limit = 10;
        int offset = int.Parse(cursor ?? "0");
        var result =
            (await services.games.GetGamesForType(CreatorType.Group, groupId, limit, offset, sortOrder, accessFilter)).ToList();
        return new RobloxCollectionPaginated<GamesForCreatorEntry>()
        {
            nextPageCursor = result.Count >= limit ? (offset+limit).ToString(): null,
            previousPageCursor = offset >= limit ? (offset-limit).ToString() : null,
            data = result,
        };
    }

    /// <summary>
    /// Endpoint is only valid for custom media (such as videos or custom thumbnails. Auto generated and/or default thumbnails are not returned.
    /// </summary>
    [HttpGet("/v1/games/{universeId}/media")]
    [HttpGet("/v2/games/{universeId}/media")]
    [HttpGet("/apisite/games/v2/games/{universeId}/media")]
    public async Task<RobloxCollection<GameMediaEntry>> GetGameMedia(long universeId) 
    {
        FeatureFlags.FeatureCheck(FeatureFlag.GamesEnabled);

        var universe = (await services.games.MultiGetUniverseInfo(new[] {universeId})).First();
        if (universe == null) {
            throw new RobloxException(RobloxException.BadRequest, 0, "Universe doesn't exist");
        }
        var result = await services.games.GetGameMedia(universe.rootPlaceId);
        return new()
        {
            data = result,
        };
    }
}