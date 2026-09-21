using Roblox.Models;
using Roblox.Models.Assets;
using Roblox.Models.Economy;
using Type = Roblox.Models.Assets.Type;

namespace Roblox.Dto.Assets;

public class CatalogSearchRequest
{
    public string? category { get; set; }
    public string? subcategory { get; set; }
    public string? sortType { get; set; }
    public string? keyword { get; set; }
    public string? cursor { get; set; }
    public int limit { get; set; } = 10;
    public CreatorType? creatorType { get; set; }
    public long? creatorTargetId { get; set; }
    public bool includeNotForSale { get; set; } = false;
    public IEnumerable<Genre>? genres { get; set; }
    public bool include18Plus { get; set; }
}

public class CatalogMultiGetEntry
{
    public string itemType { get; set; }
    public long id { get; set; }

    [System.Text.Json.Serialization.JsonIgnore]
    public bool isOnSale { get; set; }
}

public class SearchResponse : RobloxCollectionPaginated<CatalogMultiGetEntry>
{
    public SearchResponse()
    {
        data = Array.Empty<CatalogMultiGetEntry>();
    }
    // es extension
    public int _total { get; set; }
    public string? keyword { get; set; }
    public object elasticsearchDebugInfo { get; set; }
}

public class ItemRestrictions {
    public long assetId { get; set; }
    public bool isLimited { get; set; }
    public bool isLimitedUnique { get; set; }
    public bool exists { get; set; } = true;
}

public class MinimalCatalogEntry
{
    public long assetId { get; set; }
    public long? priceRobux { get; set; }
    public long? priceTickets { get; set; }
    public bool isForSale { get; set; }
    public bool isLimited { get; set; }
    public bool isLimitedUnique { get; set; }
    // Current sale total
    public long saleCount { get; set; }
    // Amount of serials available for purchase
    public long serialCount { get; set; }
    public CreatorType creatorType { get; set; }
    public long creatorId { get; set; }
    public DateTime? offsaleAt { get; set; }
    public Type assetType { get; set; }
    public bool IsFree()
    {
        return isForSale && priceRobux == 0 && priceTickets == null;
    }
}

public class UserAssetForSaleEntry
{
    public long userAssetId { get; set; }
    public long userId { get; set; }
    public string username { get; set; }
    public int? serialNumber { get; set; }
    public long price { get; set; }
    public long assetId { get; set; }
}

public class AssetResaleChartEntry
{
    public long value { get; set; }
    public DateTime date { get; set; }
}

public class RecentAveragePrice
{
    public long? recentAveragePrice { get; set; }
}

public class AssetResaleCharts
{
    public IEnumerable<AssetResaleChartEntry> priceDataPoints { get; set; }
    public IEnumerable<AssetResaleChartEntry> volumeDataPoints { get; set; }
}

public class AssetResaleData
{
    public long assetStock { get; set; }
    public long sales { get; set; }
    public long numberRemaining { get; set; }
    public long recentAveragePrice { get; set; }
    public IEnumerable<AssetResaleChartEntry> priceDataPoints { get; set; }
    public IEnumerable<AssetResaleChartEntry> volumeDataPoints { get; set; }
}

public class CatalogSearchRequest2
{
    public int? category { get; set; }
    public int? subcategory { get; set; }
    public int? sortType { get; set; }
    public string? keyword { get; set; }
    public string? cursor { get; set; }
    public int limit { get; set; } = 10;
    public CreatorType? creatorType { get; set; }
    public long? creatorTargetId { get; set; }
    public bool includeNotForSale { get; set; } = false;
    public IEnumerable<Genre>? genres { get; set; }
    public PriceOption? priceOption { get; set; }
    public CurrencyType2? currency { get; set; }
    public long? minPrice { get; set; }
    public long? maxPrice { get; set; }
}

public enum PriceOption
{
    Any = 0,
    Range,
    Free,
}

public static class CatalogCategory
{
    public const int Featured = 0;
    public const int All = 1;
    public const int Collectibles = 2;
    public const int Accessories = 11;
    public const int Clothing = 3;
    public const int BodyParts = 4;
    public const int Gears = 5;
    public const int AvatarAnimations = 12;
    public const int Emotes = 13;
    public const int Special = 69;
}

public static class CatalogSubCategory
{
    public const int All = 0;
    public const int Faces = 10;
    public const int Packages = 37;
    public const int Shirts = 12;
    public const int TeeShirts = 13;
    public const int Pants = 14;
    // GEAR
    public const int Building = 8;
    public const int Explosive = 3;
    public const int Melee = 1;
    public const int Musical = 6;
    public const int Navigation = 5;
    public const int Powerup = 4;
    public const int Ranged = 2;
    public const int Social = 7;
    public const int Transport = 9;
    //
    public const int Accessories = 19;
    public const int Animations = 39;
    public const int Gear = 5;
    public const int Emotes = 39;
    public const int Heads = 15;
    public const int FaceAccessories = 27;
    public const int Hats = 9;
    public const int Hair = 20;
    public const int Neck = 22;
    public const int Shoulder = 23;
    public const int Front = 24;
    public const int Back = 25;
    public const int Waist = 26;

    public const int Owned = 101;
    public const int Wearing = 102;
}

public class CatalogSubCategoryData
{
    public string subCategory { get; set; }
    public int subCategoryId { get; set; }
    public string? name { get; set; }
    public List<long> assetTypeIds { get; set; }
}

public class CatalogCategoryData
{
    public string category { get; set; }
    public int categoryId { get; set; }
    public string name { get; set; }
    public int orderIndex { get; set; }
    public List<CatalogSubCategoryData> subCategories { get; set; }
    public List<long> assetTypeIds { get; set; }
}

public class GenreClass
{
    public int genreId { get; set; }
    public string? name { get; set; }
    public string genre { get; set; }
}

public class CatalogNavigationClass
{
    public List<CatalogCategoryData> categories { get; set; }
    public List<GenreClass> genres { get; set; }
}
