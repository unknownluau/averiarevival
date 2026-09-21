using Microsoft.AspNetCore.Mvc;
using Roblox.Services.Exceptions;

namespace Roblox.Website.Controllers;

[ApiController]
[Route("/")]
public class Marketplace : ControllerBase
{
    [HttpGetBypass("v2/assets/{assetId:long}/details")]
    public async Task<dynamic> GetProductInfoNew(long assetId)
    {
        long remaining = 0;
        var details = await services.assets.GetAssetCatalogInfo(assetId);
        if (details.itemRestrictions.Contains("Limited") || details.itemRestrictions.Contains("LimitedUnique"))
        {
            var resale = await services.assets.GetResaleData(assetId);
            remaining = resale.numberRemaining;
        }

        try
        {
            return new
            {
                TargetId = details.id,
                AssetId = details.id,
                ProductId = details.id,
                Name = details.name,
                Description = details.description,
                AssetTypeId = (int)details.assetType,
                Creator = new
                {
                    Id = details.creatorTargetId,
                    Name = details.creatorName,
                    CreatorType = details.creatorType,
                    CreatorTargetId = details.creatorTargetId,
                },
                IconImageAssetId = 0,
                Created = details.createdAt,
                Updated = details.updatedAt,
                PriceInRobux = details.price,
                PriceInTickets = details.priceTickets,
                Sales = details.saleCount,
                IsNew = details.createdAt.Add(TimeSpan.FromDays(1)) < DateTime.Now,
                IsForSale = details.isForSale,
                IsPublicDomain = details.isForSale && details.price == 0,
                IsLimited = details.itemRestrictions.Contains("Limited"),
                IsLimitedUnique = details.itemRestrictions.Contains("LimitedUnique"),
                Remaining = remaining,
                MinimumMembershipLevel = 0,
            };
        }
        catch (RecordNotFoundException)
        {
            return Redirect($"https://economy.roproxy.com/v2/assets/{assetId}/details");
        }
    }
}
