using System.Reflection;
using Roblox.Dto.Admin;
using Roblox.Libraries.RobloxApi;
using Roblox.Services.AdminApi;

namespace Roblox.Services.Admin.Tests;

public class AdminBulkCopyPolicyTests
{
    [Fact]
    public void BulkCopyAssetRequest_DefaultsKeepLimitedAndDoNotKeepOffsale()
    {
        var request = new BulkCopyAssetRequest();

        Assert.True(request.keepLimitedProperties);
        Assert.False(request.keepOffsaleProperty);
    }

    [Fact]
    public void BulkCopyPolicy_SkipsLimitedItemsWhenRequested()
    {
        var reason = GetSkipReason(
            new ProductDataResponse { Name = "Limited Hat", IsLimited = true, IsLimitedUnique = false, IsForSale = true },
            new BulkCopyAssetRequest { skipLimitedItems = true });

        Assert.Equal("Skipped limited item", reason);
    }

    [Fact]
    public void BulkCopyPolicy_SkipsOffsaleItemsWhenRequested()
    {
        var reason = GetSkipReason(
            new ProductDataResponse { Name = "Normal Hat", IsLimited = false, IsLimitedUnique = false, IsForSale = false },
            new BulkCopyAssetRequest { skipOffsaleItems = true });

        Assert.Equal("Skipped offsale item", reason);
    }

    [Fact]
    public void BulkCopyPolicy_SkipsOpenedOffsaleGiftsWhenRequested()
    {
        var reason = GetSkipReason(
            new ProductDataResponse { Name = "Opened Gift of Something", IsLimited = false, IsLimitedUnique = false, IsForSale = false },
            new BulkCopyAssetRequest { skipOpenedOffsaleGiftItems = true });

        Assert.Equal("Skipped opened gift item", reason);
    }

    [Fact]
    public void BulkCopyPolicy_UsesLimitedPriceOnlyAsFallback()
    {
        var request = new BulkCopyAssetRequest { limitedPriceRobux = 75 };

        Assert.Equal(120, GetPrice(new ProductDataResponse { IsLimited = true, PriceInRobux = 120 }, request));
        Assert.Equal(75, GetPrice(new ProductDataResponse { IsLimited = true, PriceInRobux = null }, request));
        Assert.Equal(30, GetPrice(new ProductDataResponse { IsLimited = false, PriceInRobux = null }, request));
    }

    [Theory]
    [InlineData(100, null, 100)]
    [InlineData(100, 90, 10)]
    [InlineData(99, 50, 50)]
    [InlineData(100, -10, 100)]
    [InlineData(100, 150, 0)]
    public void BulkCopyPolicy_AppliesClampedDiscountPercent(int priceRobux, int? discountedPricePercent, int expectedPriceRobux)
    {
        Assert.Equal(expectedPriceRobux, ApplyDiscount(priceRobux, discountedPricePercent));
    }

    private static string? GetSkipReason(ProductDataResponse details, BulkCopyAssetRequest request)
    {
        var method = typeof(AdminApiService).GetMethod("GetBulkCopySkipReason", BindingFlags.NonPublic | BindingFlags.Static);
        Assert.NotNull(method);
        return (string?)method.Invoke(null, new object[] { details, request });
    }

    private static int GetPrice(ProductDataResponse details, BulkCopyAssetRequest request)
    {
        var method = typeof(AdminApiService).GetMethod("GetRobloxCopyPrice", BindingFlags.NonPublic | BindingFlags.Static);
        Assert.NotNull(method);
        return (int)method.Invoke(null, new object[] { details, request })!;
    }

    private static int ApplyDiscount(int priceRobux, int? discountedPricePercent)
    {
        var method = typeof(AdminApiService).GetMethod("ApplyRobloxCopyDiscountPercent", BindingFlags.NonPublic | BindingFlags.Static);
        Assert.NotNull(method);
        return (int)method.Invoke(null, new object?[] { priceRobux, discountedPricePercent })!;
    }
}
