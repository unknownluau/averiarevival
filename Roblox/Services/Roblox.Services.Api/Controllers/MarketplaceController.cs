using System.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.WebUtilities;
using Roblox.Dto.Games;
using Roblox.Dto.Marketplace;
using Roblox.Metrics;
using Roblox.Models.Assets;
using Roblox.Services.App.FeatureFlags;
using Roblox.Services.Exceptions;
using Roblox.Web.Infrastructure.Controllers;
using Roblox.Web.Infrastructure.Metadata;
using AssetType = Roblox.Models.Assets.Type;

namespace Roblox.Services.Api.Controllers;

[ApiController]
[Route("/")]
public class MarketplaceController : RobloxControllerBase
{
    private static RobloxException BadRequest(string message)
    {
        return new RobloxException(RobloxException.BadRequest, 0, message);
    }

    private static RobloxException UnauthorizedError()
    {
        return new RobloxException(401, 0, "Unauthorized");
    }

    [AllowRobloxAnonymous]
    [HttpGet("marketplace/productinfo")]
    public async Task<dynamic> GetProductInfo(long assetId)
    {
        try
        {
            var details = await services.assets.GetAssetCatalogInfo(assetId);
            long remaining = 0;

            if (details.itemRestrictions.Contains("Limited") ||
                details.itemRestrictions.Contains("LimitedUnique"))
            {
                var resale = await services.assets.GetResaleData(assetId);
                remaining = resale.numberRemaining;
            }

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
            return Redirect($"https://economy.roblox.com/v2/assets/{assetId}/details");
        }
    }

    [RequireRobloxSession]
    [HttpPost("marketplace/submitpurchase")]
    public async Task<dynamic> SubmitPurchase([FromForm] ProductPurchaseRequest purchaseRequest)
    {
        var userId = safeUserSession.userId;
        FeatureFlags.FeatureCheck(FeatureFlag.EconomyEnabled);
        var stopwatch = new Stopwatch();
        stopwatch.Start();

        var cachedReceipt = await services.users.GetCachedDeveloperProductPurchaseReceipt(userId, purchaseRequest.productId, purchaseRequest.requestId);
        if (cachedReceipt.HasValue)
        {
            stopwatch.Stop();
            return new
            {
                success = true,
                status = "Bought",
                receipt = cachedReceipt.Value,
            };
        }

        var productInfo = await services.games.GetDeveloperProductInfoFull(purchaseRequest.productId);
        if (!productInfo.isForSale)
            throw BadRequest("Developer Product is not for sale");

        var iconModStatus = await services.assets.GetAssetModerationStatus(productInfo.iconImageAssetId);
        if (iconModStatus != ModerationStatus.ReviewApproved)
            throw BadRequest("Developer Product is not approved");

        var universeInfo = await services.games.GetUniverseInfo(productInfo.universeId);
        if (universeInfo.rootPlaceId != purchaseRequest.placeId)
            throw BadRequest($"Place {purchaseRequest.placeId} is invalid for this purchase from universe {universeInfo.rootPlaceId} or does not exist, current place id: {currentPlaceId}");

        if (productInfo.price != purchaseRequest.expectedUnitPrice)
            throw BadRequest("Expected price is not the actual price");

        var receiptId = await services.users.PurchaseDeveloperProduct(userId, purchaseRequest.productId, purchaseRequest.requestId);
        stopwatch.Stop();
        EconomyMetrics.ReportPurchaseDuration(stopwatch.ElapsedMilliseconds, PurchaseProductType.DeveloperProduct, false);

        return new
        {
            success = true,
            status = "Bought",
            receipt = receiptId,
        };
    }

    [RequireRobloxSession]
    [HttpPost("marketplace/purchase")]
    public async Task<dynamic> PurchaseProductMarket([FromForm] PurchaseRequest purchaseRequest)
    {
        FeatureFlags.FeatureCheck(FeatureFlag.EconomyEnabled);
        var stopwatch = new Stopwatch();
        stopwatch.Start();

        var productInfo = await services.assets.GetProductForAsset(purchaseRequest.productId);
        if (purchaseRequest.productId is 0 or < 0)
            purchaseRequest.productId = 0;

        if (productInfo.isLimited || productInfo.isLimitedUnique)
            throw BadRequest("Cannot purchase limited or limited unique items through this endpoint");

        await services.users.PurchaseNormalItem(safeUserSession.userId, purchaseRequest.productId, purchaseRequest.currencyTypeId);
        stopwatch.Stop();
        EconomyMetrics.ReportPurchaseDuration(stopwatch.ElapsedMilliseconds, PurchaseProductType.Asset, false);

        return new
        {
            success = true,
            status = "Bought",
            receipt = "test",
        };
    }

    [AllowRobloxAnonymous]
    [HttpGet("marketplace/productdetails")]
    public async Task<dynamic> GetProductDetailsMarketplace(long productId)
    {
        try
        {
            var details = await services.games.GetDeveloperProductInfoFull(productId);
            return new
            {
                TargetId = details.universeId,
                AssetId = 0,
                ProductId = details.id,
                ProductType = "Developer Product",
                Name = details.name,
                Description = details.description,
                AssetTypeId = 0,
                Creator = new
                {
                    Id = 0,
                    Name = (string?)null,
                    CreatorType = details.creatorType,
                    CreatorTargetId = details.creatorId,
                },
                IconImageAssetId = details.iconImageAssetId,
                Created = details.createdAt,
                Updated = details.updatedAt,
                PriceInRobux = details.price,
                PriceInTickets = (int?)null,
                Sales = details.sales,
                IsNew = details.createdAt.Add(TimeSpan.FromDays(1)) < DateTime.Now,
                IsForSale = details.isForSale,
                IsPublicDomain = details.isForSale && details.price == 0,
                IsLimited = false,
                IsLimitedUnique = false,
                Remaining = (int?)null,
                MinimumMembershipLevel = 0,
            };
        }
        catch (RecordNotFoundException)
        {
            var asset = await services.assets.DoesAssetExistType(productId);
            if (asset.exists)
            {
                return asset.assetType switch
                {
                    (int)AssetType.GamePass => Redirect($"/marketplace/game-pass-product-info?gamePassId={productId}"),
                    _ => Redirect($"/marketplace/productinfo?assetId={productId}"),
                };
            }
        }

        throw BadRequest("Asset " + productId + " does not exist.");
    }

    [AllowRobloxAnonymous]
    [HttpGet("marketplace/game-pass-product-info")]
    public async Task<dynamic> GetPassInfo(long gamePassId)
    {
        var details = await services.assets.GetAssetCatalogInfo(gamePassId);

        if (details.assetType != AssetType.GamePass)
            throw BadRequest("Asset " + gamePassId + " is not a Game Pass");

        var gamePassDetails = await services.games.GetGamePassInfo(gamePassId);
        return new
        {
            TargetId = await services.games.GetRootPlaceId(gamePassDetails.universeId),
            ProductType = "Game Pass",
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
            IconImageAssetId = details.id,
            Created = details.createdAt,
            Updated = details.updatedAt,
            PriceInRobux = details.price,
            PriceInTickets = details.priceTickets,
            Sales = details.saleCount,
            IsNew = details.createdAt.Add(TimeSpan.FromDays(1)) < DateTime.Now,
            IsForSale = details.isForSale,
            IsPublicDomain = details.isForSale && details.price == 0,
            IsLimited = false,
            IsLimitedUnique = false,
            Remaining = 0,
            MinimumMembershipLevel = 0,
            ContentRatingTypeId = 0,
        };
    }

    [RequireRccRequest]
    [HttpPost("marketplace/validatepurchase")]
    public async Task<ReceiptResponse> ValidatePurchase(Guid receipt)
    {
        if (!isRCC)
            throw UnauthorizedError();

        var productReceipt = await services.games.GetProductReceipt(receipt);
        if (productReceipt == null)
            throw BadRequest("Receipt is invalid or does not exist.");

        return new ReceiptResponse
        {
            playerId = productReceipt.userId,
            placeId = currentPlaceId,
            isValid = productReceipt.processed,
            productId = productReceipt.productId,
        };
    }

    [RequireRccRequest]
    [HttpGet("gametransactions/getpendingtransactions")]
    public async Task<dynamic> GetPendingTransactions(long placeId, long playerId)
    {
        if (!isRCC)
            throw UnauthorizedError();

        var universeId = await services.games.GetUniverseId(placeId);
        var pendingReceipts = await services.games.GetPendingProductReceipts(playerId, universeId);

        if (pendingReceipts is null)
            return Array.Empty<dynamic>();

        return pendingReceipts.Select(pendingReceipt => new
        {
            playerId,
            placeId,
            receipt = pendingReceipt.id,
            actionArgs = new List<dynamic>
            {
                new
                {
                    Key = "productId",
                    Value = pendingReceipt.productId,
                },
                new
                {
                    Key = "currencyTypeId",
                    Value = 1,
                },
                new
                {
                    Key = "unitPrice",
                    Value = pendingReceipt.price,
                },
            },
        }).ToArray();
    }

    [RequireRccRequest]
    [HttpPost("gametransactions/settransactionstatuscomplete")]
    public async Task<dynamic> ProcessTransaction()
    {
        if (!isRCC)
            throw UnauthorizedError();

        string? receiptValue = null;
        if (Request.HasFormContentType)
        {
            var form = await Request.ReadFormAsync();
            receiptValue = form["receipt"].FirstOrDefault();
        }

        if (string.IsNullOrWhiteSpace(receiptValue))
        {
            var requestBody = await GetRequestBody();
            if (!string.IsNullOrWhiteSpace(requestBody))
            {
                var parsed = QueryHelpers.ParseQuery(requestBody);
                receiptValue = parsed.TryGetValue("receipt", out var receiptValues)
                    ? receiptValues.FirstOrDefault()
                    : null;
            }
        }

        if (!Guid.TryParse(receiptValue, out var receiptId))
            throw BadRequest("Receipt is invalid or does not exist.");

        var receipt = await services.games.GetProductReceipt(receiptId);
        if (receipt == null)
            throw BadRequest("Receipt is invalid or does not exist.");

        if (receipt.processed)
        {
            return new
            {
                success = true,
            };
        }

        await services.games.ProcessProductReceipt(receiptId);

        return new
        {
            success = true,
        };
    }
}
