using Microsoft.AspNetCore.Mvc;
using Roblox.Dto.Games;
using Roblox.Dto.Users;
using Roblox.Logging;
using Roblox.Models.Assets;
using Roblox.Services.App.FeatureFlags;
using Roblox.Services.Exceptions;
using Roblox.Web.Infrastructure.Controllers;
using Roblox.Web.Infrastructure.Metadata;
using Type = Roblox.Models.Assets.Type;

namespace Roblox.Services.Api.Controllers;

[ApiController]
[Route("/")]
public class UniversesController : RobloxControllerBase
{
    [AllowRobloxAnonymous]
    [HttpGet("universes/get-universe-containing-place")]
    public async Task<dynamic> GetUniverseContainingPlace(long placeid)
    {
        return new
        {
            UniverseId = await services.games.GetUniverseId(placeid),
        };
    }

    [RequireRccRequest]
    [HttpGet("v1.1/game-start-info")]
    public async Task<dynamic> GameStartInfo(long universeId)
    {
        var universe = await services.games.GetUniverseInfo(universeId);
        return new
        {
            gameAvatarType = universe.universeAvatarType,
            allowCustomAnimations = "True",
            universeAvatarCollisionType = "OuterBox",
            universeAvatarBodyType = "Standard",
            jointPositioningType = "ArtistIntent",
            universeAvatarMinScales = new
            {
                height = 0.9,
                width = 0.7,
                head = 0.95,
                depth = 0.0,
                proportion = 0.0,
                bodyType = 0.0,
            },
            universeAvatarMaxScales = new
            {
                height = 1.05,
                width = 1.0,
                head = 1.0,
                depth = 0.0,
                proportion = 1.0,
                bodyType = 1.0,
            },
            universeAvatarAssetOverrides = new List<object>(),
        };
    }
    
    [RequireRccRequest]
    [HttpPost("game/load-place-info")]
    public async Task<dynamic> LoadPlaceInfo()
    {
        var details = await services.assets.GetAssetCatalogInfo(currentPlaceId);
        return new
        {
            CreatorId =  details.creatorTargetId,
            CreatorType = "User",
            PlaceVersion = details.id,
            GameId = currentPlaceId,
            IsRobloxPlace = details.creatorTargetId == 1
        };
    }
    
    [AllowRobloxAnonymous]
    [HttpGet("developerproducts/list")]
    public async Task<dynamic> GetDeveloperProducts(long page, long? placeId, long? universeId)
    {
        if (page is < 1 or > 5)
        {
            page = 1;
        }

        universeId = universeId switch
        {
            null when placeId is not null => await services.games.GetUniverseId(placeId.Value),
            null => throw new RobloxException(RobloxException.BadRequest, 0,
                "You must provide a valid placeId or universeId."),
            _ => universeId!
        };

        var products = (await services.games.GetDeveloperProducts(universeId.Value, 5, 5 * (page - 1))).ToList();
        return new
        {
            FinalPage = products.Count < 5 || page == 5,
            DeveloperProducts = products.Select(c => new
            {
                ProductId = c.id,
                DeveloperProductId = c.iconImageAssetId,
                Name = c.name,
                Description = c.description,
                IconImageAssetId = c.iconImageAssetId,
                displayName = c.name,
                displayDescription = c.description,
                displayIcon = (int?)null,
                PriceInRobux = c.priceInRobux,
            }),
            PageSize = products.Count
        };
    }
    
    // TODO: does this start with game? does this have a POST variant? (was requested with GET, and /universes/)
    [HttpGet("game/validate-place-join")]
    [HttpPost("universes/validate-place-join")]
    [HttpGet("universes/validate-place-join")]
    public async Task<string> ValidateJoin(long originPlaceId, long destinationPlaceId)
    {
        using var playerSecurity = ServiceProvider.GetOrCreate<PlayerSecurityService>();
        if (await playerSecurity.ValidateTeleport(originPlaceId, destinationPlaceId))
        {
            return "true";
        }
        await services.discordBotApi.SendMessageInChannel(Configuration.DiscordLogChannelId, $"[RAGE-SS] Flag: InvalidTeleport\nOrigin Place Id:{originPlaceId}\nDestination Place Id: {destinationPlaceId}");
        return "false";
    }
    
    [RequireRobloxSession]
    [HttpPost("universes/{universeId:long}/enablecloudedit")]
    public async Task<IActionResult> EnableCloudEdit(long universeId)
    {
        await services.games.CanManageUniverse(safeUserSession.userId, universeId);
        await services.games.SetCloudedit(true, universeId);
        return Ok(new { });
    }

    [HttpGet("universes/{universeId:long}/cloudeditenabled")]
    public async Task<dynamic> IsCloudEditEnabled(long universeId)
    {
        return new
        {
            enabled = await services.games.IsCloudeditEnabled(universeId)
        };
    }

    [RequireRobloxSession]
    [HttpGet("universes/get-info")]
    public async Task<dynamic> GetUniverseInfo(long universeId)
    {
        var universe = (await services.games.MultiGetUniverseInfo([universeId])).FirstOrDefault();
        if (universe == null)
        {
            throw new RecordNotFoundException();
        }

        return new
        {
            Name = universe.name,
            Description = universe.description,
            RootPlace = universe.rootPlaceId,
            StudioAccessToApisAllowed = true,
            CurrentUserHasEditPermissions = universe.creatorId == safeUserSession.userId,
            UniverseAvatarType = universe.universeAvatarType,
        };
    }
    
    [RequireRobloxSession]
    [HttpGet("/v1/universes/{universeId:long}/symbolic-links")]
    public dynamic GetBoilerplateContent(long universeId)
    {
        return new
        {
            previousPageCursor = (string?)null,
            nextPageCursor = (string?)null,
            data =  Array.Empty<string>()
        };
    }

    [RequireRobloxSession]
    [HttpGet("universes/get-universe-places")]
    public async Task<dynamic> GetUniversePlaces(long universeId)
    {
        await services.games.CanManageUniverse(safeUserSession.userId, universeId);
        var rootPlace = await services.games.GetRootPlaceId(universeId);
        var places = (await services.games.GetUniversePlaces(universeId)).ToList();
        return new
        {
            FinalPage = true,
            RootPlace = rootPlace,
            Places = places.Select(placeInfo => new
            {
                PlaceId = placeInfo.placeId,
                Name = placeInfo.name,
            }),
            PageSize = places.Count,
        };
    }

    [AllowRobloxAnonymous]
    [HttpGet("universes/get-aliases")]
    public dynamic GetAliases()
    {
        return new
        {
            FinalPage = true,
            Aliases = new List<string>(),
            PageSize = 50,
        };
    }
    
    [RequireRobloxSession]
    [HttpPost("/universes/create")]
    public async Task<dynamic> CreateUniverse([FromBody] CreateUniverseRequest request)
    {
        FeatureFlags.FeatureCheck(FeatureFlag.CreatePlaceSelfService);
        await using var createGameLock =
            await Cache.redLock.CreateLockAsync("CreatePlaceSelfServiceV1:UserId:" + safeUserSession.userId,
                TimeSpan.FromSeconds(10));
        if (!createGameLock.IsAcquired)
            throw new RobloxException(RobloxException.TooManyRequests, 0, "Too many attempts. Try again in a few seconds.");
        
        var createStatus = await CanCreatePlace(safeUserSession.userId);
        if (createStatus != PlaceCreationFailureReason.Ok) 
            throw new RobloxException(RobloxException.BadRequest, 0, GetMessage(createStatus));
        

        // create one!
        var asset = await services.assets.CreatePlace(safeUserSession.userId, safeUserSession.username, CreatorType.User, safeUserSession.userId, request.templatePlaceIdToUse);
        // create universe too
        var universe = await services.games.CreateUniverse(asset.placeId);
        // give url
        return new
        {
            asset.placeId,
            universe.universeId,
        };
    }
    
    private async Task<PlaceCreationFailureReason> CanCreatePlace(long userId)
    {
        var userInfo = await services.users.GetUserById(userId);
        if (userInfo.created > DateTime.UtcNow.Subtract(TimeSpan.FromDays(1)))
            return PlaceCreationFailureReason.AccountTooNew;

        var createdPlaces = (await services.assets.GetCreations(CreatorType.User, userId, Type.Place, 0, 100)).ToArray();
        if (createdPlaces.Length != 0)
        {
            if (createdPlaces.Length > 15)
                return PlaceCreationFailureReason.TooManyPlaces;

            var placeDetails = (await services.games.MultiGetPlaceDetails(createdPlaces
                    .Select(c => c.assetId)))
                .ToArray();

            if (placeDetails.Length != createdPlaces.Length)
                if (placeDetails.Length == 0)
                    throw new Exception("Place details len is zero while createdPlaces len is not zero");


            var isAnyPlaceCreatedLessThanADayAgo =
                placeDetails.FirstOrDefault(v => v.created > DateTime.UtcNow.Subtract(TimeSpan.FromDays(1))) != null;

            if (isAnyPlaceCreatedLessThanADayAgo && !(userId is 3 or 1 or 7))
                return PlaceCreationFailureReason.LatestPlaceCreatedTooRecently;
        }


        var app = await services.users.GetApplicationByUserId(userId);
        return app is not {status: UserApplicationStatus.Approved} ? 
            PlaceCreationFailureReason.NoApplication : 
            PlaceCreationFailureReason.Ok;
    }
    
    private static string GetMessage(PlaceCreationFailureReason reason)
    {
        return reason switch
        {
            PlaceCreationFailureReason.AccountTooNew =>
                "Your account is too new. Try again when your account is at least 7 days old.",
            PlaceCreationFailureReason.TooManyPlaces => 
                "Your account already has the maximum amount of places on it.",
            PlaceCreationFailureReason.NoApplication => 
                "You cannot create a place if you did not join through the application system.",
            PlaceCreationFailureReason.TooInactive => 
                "Your account is too inactive to create a place. " +
                "Staff cannot comment on the exact reason, so please do not ask. " +
                "Try playing around some more, posting on places like the forums, " +
                "joining groups, buying items, then try again in a few days.",
            PlaceCreationFailureReason.LatestPlaceCreatedTooRecently => 
                "Latest place was created too recently. Try again in a day.",
            PlaceCreationFailureReason.NotEnoughVisitsForNewPlace => 
                "You do not have enough visits to create a new place. Try again in a few days.",
            _ => "Unknown reason. Code = " + reason,
        };
    }

    private enum PlaceCreationFailureReason
    {
        Ok = 1,
        AccountTooNew,
        TooManyPlaces,
        NoApplication,
        TooInactive,
        LatestPlaceCreatedTooRecently,
        NotEnoughVisitsForNewPlace,
    }
}
