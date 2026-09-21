using Microsoft.AspNetCore.Mvc;
using Roblox.Dto.Assets;
using Roblox.Dto.Games;
using Roblox.Exceptions;
using Roblox.Models;
using Roblox.Models.Assets;
using Roblox.Models.Economy;
using Roblox.Services.App.FeatureFlags;
using Roblox.Services.Exceptions;
using Roblox.Web.Infrastructure.Metadata;

namespace Roblox.Website.Controllers;

[ApiController]
[RequireRobloxSession]
[Route("/apisite/catalog/v3")]
public class CatalogControllerV3 : ControllerBase
{
    [HttpGet("navigation-menu-items")]
    public dynamic GetNavigationMenuItems()
    {
        return new CatalogNavigationClass
        {
            categories = services.assets.CatalogNavigation,
            genres = services.assets.CatalogGenres,
        };
    }

    [HttpGet("search/items")]
    public async Task<SearchResponse> SearchItems(
        int? category,
        int? subcategory,
        int? sortType,
        string? keyword,
        string? cursor,
        int limit = 10,
        CreatorType? creatorType = null,
        long? creatorTargetId = null,
        string? creatorName = null,
        bool includeNotForSale = false,
        string? _genreFilterCsv = null,
        string? genres = null,
        PriceOption? priceOption = null,
        CurrencyType2? currency = null,
        long? minPrice = null,
        long? maxPrice = null
        )
    {
        if (FeatureFlags.IsDisabled(FeatureFlag.EconomyEnabled))
            return new SearchResponse();
		
        if (!await services.cooldown.TryIncrementBucketCooldown("Catalog:V3:Search:Items:Ip:" + GetIP(), 40, TimeSpan.FromMinutes(1)))
            throw new TooManyRequestsException();
        
        var request = new CatalogSearchRequest2
        {
            category = category,
            keyword = keyword,
            subcategory = subcategory,
            sortType = sortType,
            cursor = cursor,
            limit = limit,
            creatorType = creatorType,
            creatorTargetId = creatorTargetId,
            includeNotForSale = includeNotForSale,
            genres = _genreFilterCsv?.Split(",").Select(Enum.Parse<Genre>) ?? genres?.Split(",").Select(Enum.Parse<Genre>),
            priceOption = priceOption,
            minPrice = minPrice,
            maxPrice = maxPrice,
            currency = currency,
        };
        if (request.limit is > 100 or < 1) request.limit = 10;
        if (priceOption != PriceOption.Range || minPrice == null || maxPrice == null || minPrice > maxPrice || maxPrice < minPrice || maxPrice < 0)
        {
            if (minPrice != null && minPrice >= 0 && (request.maxPrice ?? 0) == 0)
            {
                request.maxPrice = long.MaxValue;
            }
            else
            {
                request.minPrice = null;
                request.maxPrice = null;
            }
        }

        if (creatorTargetId == null && creatorName != null)
        {
            long targetId;
            switch (request.creatorType)
            {
                case CreatorType.User:
                    targetId = await services.users.GetUserIdFromUsername(creatorName);
                    break;
                case CreatorType.Group:
                    targetId = await services.groups.GetGroupIdByName(creatorName);
                    break;
                default:
                    try
                    {
                        targetId = await services.users.GetUserIdFromUsername(creatorName);
                    }
                    catch (RecordNotFoundException)
                    {
                        targetId = await services.groups.GetGroupIdByName(creatorName);
                    }
                    break;
            }
            request.creatorTargetId = targetId;
        }

        return await services.assets.SearchCatalog2(request);
    }
}