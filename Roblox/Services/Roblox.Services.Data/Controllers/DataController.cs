using Microsoft.AspNetCore.Mvc;
using Roblox.Services.App.FeatureFlags;
using Roblox.Services.Exceptions;
using Roblox.Web.Infrastructure.Controllers;
using Roblox.Web.Infrastructure.Metadata;
using Type = Roblox.Models.Assets.Type;

namespace Roblox.Services.Data.Controllers;

[ApiController]
[InternalServiceOnly]
[Route("/")]
public sealed class DataController : RobloxControllerBase
{
    [HttpPost("Data/Upload.ashx")]
    [RequireRobloxSession]
    public async Task<long> UploadPlaceFromStudio(long assetId)
    {
        FeatureFlags.FeatureCheck(FeatureFlag.UploadContentEnabled);

        if (!await services.assets.CanUserModifyItem(assetId, safeUserSession.userId))
        {
            throw new RobloxException(RobloxException.Forbidden, 1, "Not allowed to upload");
        }

        var assetInfo = await services.assets.GetAssetCatalogInfo(assetId);
        if (assetInfo.assetType != Type.Place &&
            assetInfo.assetType != Type.Animation &&
            assetInfo.assetType != Type.Model)
        {
            throw new RobloxException(RobloxException.BadRequest, 0, "This asset type is not supported");
        }

        using var assetStream = await GetRequestBodyAsMemoryStream();
        using var validationStream = new MemoryStream();
        await assetStream.CopyToAsync(validationStream);
        validationStream.Position = 0;

        if (!await services.assets.ValidateAssetFile(validationStream, assetInfo.assetType))
        {
            throw new RobloxException(RobloxException.BadRequest, 0, "Invalid asset file");
        }

        assetStream.Position = 0;
        await services.assets.CreateAssetVersion(assetId, assetInfo.creatorTargetId, assetStream);
        return assetId;
    }
}
