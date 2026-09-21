using System.Text.Json;
using Roblox.Libraries.RobloxApi;
using AssetType = Roblox.Models.Assets.Type;

namespace Roblox.Services.Admin.Tests;

public class RobloxApiBatchMappingTests
{
    [Fact]
    public void CatalogItemDetails_MapsBulkCopyFields()
    {
        const string json = "{\"data\":[{\"id\":22118646,\"itemType\":\"Asset\",\"name\":\"Opened Gift Hat\",\"description\":\"Roblox description\",\"assetType\":8,\"price\":null,\"lowestPrice\":125,\"priceStatus\":\"Off Sale\",\"itemRestrictions\":[\"LimitedUnique\"],\"creatorType\":\"User\",\"creatorTargetId\":1}]}";

        var response = JsonSerializer.Deserialize<MultiGetDetailsResponse>(json);
        var entry = Assert.Single(response!.data);
        var details = entry.ToProductDataResponse();

        Assert.True(entry.HasBulkCopyRequiredFields());
        Assert.Equal(22118646, entry.id);
        Assert.Equal("Opened Gift Hat", details.Name);
        Assert.Equal("Roblox description", details.Description);
        Assert.Equal(AssetType.Hat, details.AssetTypeId);
        Assert.Equal(125, details.PriceInRobux);
        Assert.False(details.IsForSale);
        Assert.True(details.IsLimited);
        Assert.True(details.IsLimitedUnique);
    }

    [Fact]
    public void CatalogItemDetails_MissingRequiredFieldsCanFallback()
    {
        var entry = new MultiGetDetailsResponseEntry
        {
            id = 22118646,
            name = "Missing restriction field",
            description = "Description",
            assetType = (int)AssetType.Hat,
        };

        Assert.False(entry.HasBulkCopyRequiredFields());
    }

    [Fact]
    public void AssetDeliveryV2Batch_ParsesFirstUsableLocation()
    {
        const string json = "[{\"requestId\":\"22118646\",\"assetTypeId\":8,\"locations\":[{\"assetFormat\":\"source\",\"location\":\"https://assetdelivery.roblox.com/example\"}]}]";

        var response = JsonSerializer.Deserialize<AssetDeliveryV2BatchResponse[]>(json);
        var entry = Assert.Single(response!);

        Assert.Equal("22118646", entry.requestId);
        Assert.Equal("https://assetdelivery.roblox.com/example", entry.FirstUsableLocation());
    }
}
