// ReSharper disable InconsistentNaming

using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Http;
using Roblox.Dto.AbuseReport;
using Roblox.Dto.Assets;
using Roblox.Dto.Economy;
using Roblox.Dto.Groups;
using Roblox.Dto.Trades;
using Roblox.Dto.Users;
using Roblox.Models.AbuseReport;
using Roblox.Models.Assets;
using Roblox.Models.Economy;
using Roblox.Models.Staff;
using Roblox.Models.Trades;
using Type = Roblox.Models.Assets.Type;

namespace Roblox.Dto.Admin;

public class SetAlertRequest
{
    public string? text { get; set; }
    public string? url { get; set; }
}

public sealed class AdminActorContext
{
    public long userId { get; set; }
    public string sessionId { get; set; } = string.Empty;
    public bool isOwner { get; set; }
    public IReadOnlyCollection<Access> permissions { get; set; } = Array.Empty<Access>();
}

public sealed class AdminDataRow : Dictionary<string, object?>
{
}

public sealed class AdminPermissionsResponse
{
    public AdminRankResponse rank { get; set; } = new();
}

public sealed class AdminRankResponse
{
    public string? name { get; set; }
    public AdminRankDetailsResponse details { get; set; } = new();
    public IEnumerable<Access> permissions { get; set; } = Array.Empty<Access>();
}

public sealed class AdminRankDetailsResponse
{
    public bool isAdmin { get; set; }
    public bool isModerator { get; set; }
    public bool isOwner { get; set; }
}

public sealed class AdminStatsResponse
{
    public AdminMemoryStatsResponse memory { get; set; } = new();
    public long serverStartTime { get; set; }
}

public sealed class AdminMemoryStatsResponse
{
    public string allocated { get; set; } = string.Empty;
    public string used { get; set; } = string.Empty;
}

public sealed class AdminSystemMessageResponse
{
    public string LinkText { get; set; } = string.Empty;
    public string LinkUrl { get; set; } = string.Empty;
    public string Text { get; set; } = string.Empty;
    public bool IsVisible { get; set; }
}

public sealed class AdminMessageResponse
{
    public string message { get; set; } = string.Empty;
}

public sealed class AdminCountResponse
{
    public long count { get; set; }
}

public sealed class AdminTotalResponse
{
    public long total { get; set; }
}

public sealed class AdminRobuxAmountResponse
{
    public int robuxAmount { get; set; }
}

public sealed class AdminDateResponse
{
    public DateTime? date { get; set; }
}

public sealed class AdminUsersResponse
{
    public IEnumerable<AdminDataRow> data { get; set; } = Array.Empty<AdminDataRow>();
}

public sealed class AdminModerationLogsResponse
{
    public IEnumerable<AdminDataRow> data { get; set; } = Array.Empty<AdminDataRow>();
    public IEnumerable<string> columns { get; set; } = Array.Empty<string>();
}

public sealed class AdminGroupModerationInfoResponse
{
    public AdminDataRow? icon { get; set; }
    public AdminDataRow? info { get; set; }
}

public sealed class AdminAssetDetailsResponse
{
    public IEnumerable<MultiGetAssetDeveloperDetails> developerInfo { get; set; } = Array.Empty<MultiGetAssetDeveloperDetails>();
    public IEnumerable<Roblox.Dto.Assets.MultiGetEntry> info { get; set; } = Array.Empty<Roblox.Dto.Assets.MultiGetEntry>();
}

public sealed class AdminAssetIdResponse
{
    public long assetId { get; set; }
}

public sealed class BulkCopyAssetRequest
{
    public IEnumerable<long> assetIds { get; set; } = Array.Empty<long>();
    public bool force { get; set; }
    public bool skipLimitedItems { get; set; }
    public bool keepLimitedProperties { get; set; } = true;
    public int? limitedPriceRobux { get; set; }
    public bool skipOpenedOffsaleGiftItems { get; set; }
    public bool skipOffsaleItems { get; set; }
    public bool keepOffsaleProperty { get; set; }
    public int? discountedPricePercent { get; set; }
}

public sealed class BulkCopyAssetResult
{
    public long robloxAssetId { get; set; }
    public long? assetId { get; set; }
    public string? catalogUrl { get; set; }
    public int? priceRobux { get; set; }
    public bool alreadyExisted { get; set; }
    public bool success { get; set; }
    public string? error { get; set; }
}

public sealed class BulkCopyAssetResponse
{
    public IEnumerable<BulkCopyAssetResult> results { get; set; } = Array.Empty<BulkCopyAssetResult>();
    public IEnumerable<string> catalogUrls { get; set; } = Array.Empty<string>();
}

public sealed class AdminCreateGameResponse
{
    public long placeId { get; set; }
    public long universeId { get; set; }
}

public sealed class AdminLotteryRunResponse
{
    public string name { get; set; } = string.Empty;
    public string username { get; set; } = string.Empty;
}

public sealed class AdminSuccessResponse
{
    public bool success { get; set; }
}

public sealed class AdminLatestTextModerationIdsResponse
{
    public long ForumPost { get; set; }
    public long AssetComment { get; set; }
    public long GroupWallPost { get; set; }
    public long UserStatusPost { get; set; }
    public long GroupStatusPost { get; set; }
}

public sealed class AdminApplicationApproveResponse
{
    public string? joinId { get; set; }
}

public sealed class AdminTradeHistoryResponse
{
    public TradeEntryDb trade { get; set; } = new();
    public TradeEntryDbFull? db { get; set; }
    public IEnumerable<TradeItemEntryDb> items { get; set; } = Array.Empty<TradeItemEntryDb>();
}

public sealed class AdminTrackedItemHistoryEntry
{
    public DateTime created_at { get; set; }
    public string track_type { get; set; } = string.Empty;
    public long? user_id_two { get; set; }
    public long? user_id_one { get; set; }
    public string? user_one_username { get; set; }
    public string? user_two_username { get; set; }
    public long? amount { get; set; }
    public int? currency_type { get; set; }
    public long? id { get; set; }
}

public sealed class AdminMacAddressHistoryEntry
{
    public long userId { get; set; }
    public string macAddress { get; set; } = string.Empty;
    public DateTimeOffset createdAt { get; set; }
    public DateTimeOffset updatedAt { get; set; }
}

public sealed class AdminAltAccountByMacEntry
{
    public string macAddress { get; set; } = string.Empty;
    public long userCount { get; set; }
    public IEnumerable<AdminAltAccountUserEntry> users { get; set; } = Array.Empty<AdminAltAccountUserEntry>();
}

public sealed class AdminAltAccountUserEntry
{
    public long id { get; set; }
    public string username { get; set; } = string.Empty;
    public string status { get; set; } = string.Empty;
}

public sealed class AdminIdentitySearchEntry
{
    public long id { get; set; }
    public string username { get; set; } = string.Empty;
    public string status { get; set; } = string.Empty;
    public IEnumerable<string> macAddresses { get; set; } = Array.Empty<string>();
    public IEnumerable<string> actions { get; set; } = Array.Empty<string>();
    public DateTimeOffset? createdAt { get; set; }
    public DateTimeOffset? updatedAt { get; set; }
}

public sealed class AdminIpBanStatusResponse
{
    public string ipHash { get; set; } = string.Empty;
    public bool isBanned { get; set; }
}

public sealed class AdminIpBanRequest
{
    [Required, StringLength(128, MinimumLength = 1)]
    public string ipHash { get; set; } = string.Empty;

    [Required, StringLength(4096, MinimumLength = 3)]
    public string internalReason { get; set; } = string.Empty;
}

public sealed class AdminUserAltAccountsResponse
{
    public int sourceMacCount { get; set; }
    public IEnumerable<AdminAltAccountScoreEntry> data { get; set; } = Array.Empty<AdminAltAccountScoreEntry>();
}

public sealed class AdminAltAccountScoreEntry
{
    public long id { get; set; }
    public string username { get; set; } = string.Empty;
    public string status { get; set; } = string.Empty;
    public int score { get; set; }
    public string evidenceLevel { get; set; } = string.Empty;
    public bool exactMacSet { get; set; }
    public int sharedMacCount { get; set; }
    public int candidateMacCount { get; set; }
    public int sharedIpHashCount { get; set; }
}

public sealed class AdminUserBanHistoryEntry
{
    public long id { get; set; }
    public long user_id { get; set; }
    public string reason { get; set; } = string.Empty;
    public string internal_reason { get; set; } = string.Empty;
    public string created_at { get; set; } = string.Empty;
    public string? expired_at { get; set; }
    public long actor_id { get; set; }
    public string actor_username { get; set; } = string.Empty;
}

public sealed class AdminChatMessagesResponse
{
    public string content { get; set; } = string.Empty;
    public string contentType { get; set; } = "text/plain; charset=utf-8";
}

public sealed class ModerateUgcRequestBody
{
    public long id { get; set; }
    public bool isApproved { get; set; }
}

public sealed class PendingUgcRequestEntry
{
    public long id { get; set; }
    public long userId { get; set; }
    public long robloxAssetId { get; set; }
    public string robloxUrl { get; set; } = string.Empty;
    public string? itemName { get; set; }
    public DateTime createdAt { get; set; }
    public string? creatorName { get; set; }
    public short status { get; set; }
}

public class CreateUserRequest
{
    public string? username { get; set; }
    public string? password { get; set; }
    public string? userId { get; set; }
}

public class GiftUsersRequest
{
    public long giftId { get; set; }
    public long assetId { get; set; }
}

public class ForceApplicationReq
{
    public long userId { get; set; }
    public string? socialURL { get; set; }
}

public class PendingAssetEntry
{
    public long id { get; set; }
    public string name { get; set; } = string.Empty;
    public string? content_url { get; set; }
    public long creatorId { get; set; }
    public string creatorName { get; set; } = string.Empty;
    public Type assetType { get; set; }
}

public class ModerateAssetRequest
{
    public long assetId { get; set; }
    public bool isApproved { get; set; }
    public bool is18Plus { get; set; }
}

public class AssetModerationStatus
{
    public long? robloxAssetId { get; set; }
    public ModerationStatus moderationStatus { get; set; }

    public bool canEarnRobuxFromApproval =>
        moderationStatus is ModerationStatus.AwaitingApproval or ModerationStatus.AwaitingModerationDecision;
}

public class ModerateIconRequest
{
    public long iconId { get; set; }
    public bool isApproved { get; set; }
    public bool is18Plus { get; set; }
}

public class IconToggleRequest
{
    public string? name { get; set; }
    public long groupId { get; set; }
    public int approved { get; set; }
}

public class BanUserRequest
{
    public long userId { get; set; }
    public string reason { get; set; } = string.Empty;
    public string? internalReason { get; set; }
    public string? expires { get; set; }
    public bool isMachineBan { get; set; }
}

public class CreateMessageRequest
{
    public long userId { get; set; }
    public string subject { get; set; } = string.Empty;
    public string body { get; set; } = string.Empty;
}

public class UserIdRequest
{
    public long userId { get; set; }
}

public class GiveBadgeRequest
{
    public long badgeId { get; set; }
    public long userId { get; set; }
}

public class GiveUserTicketsRequest
{
    public long userId { get; set; }
    public long tickets { get; set; }
}

public class GiveUserRobuxRequest
{
    public long userId { get; set; }
    public long robux { get; set; }
}

public class RemoveItemRequest
{
    public long userId { get; set; }
    public long userAssetId { get; set; }
}

public class GiveItemRequest
{
    public long userId { get; set; }
    public long assetId { get; set; }
    public int copies { get; set; } = 1;
    public bool giveSerial { get; set; } = false;
}

public class DeleteUsernameRequest
{
    public string username { get; set; } = string.Empty;
    public long userId { get; set; }
}

public class DeleteForumPostRequest
{
    public long postId { get; set; }
}

public class ReRenderRequest
{
    public long assetId { get; set; }
}

public class FixBuggedRendersRequest
{
    public int? limit { get; set; }
    public bool newestFirst { get; set; }

    [Required, RegularExpression("^(roblox|user)$")]
    public string ownership { get; set; } = "roblox";
}

public sealed class FixBuggedRendersResponse
{
    public int matchedCount { get; set; }
    public IEnumerable<long> rerenderedAssetIds { get; set; } = Array.Empty<long>();
}

public class UpdateProductRequest
{
    public long assetId { get; set; }
    public string description { get; set; } = string.Empty;
    public string assetName { get; set; } = string.Empty;
    public bool isForSale { get; set; }
    public bool isLimited { get; set; }
    public bool isLimitedUnique { get; set; }
    public int? priceRobux { get; set; }
    public int? priceTickets { get; set; }
    public int? maxCopies { get; set; }
    public DateTime? offsaleDeadline { get; set; }
}

public class StartSaleRequest
{
    public long assetId { get; set; }
    public int? pctOff { get; set; }
    public int? flatRobux { get; set; }
    public int? flatTix { get; set; }
    public long salesUnits { get; set; }
}

public class EndSaleRequest
{
    public long assetId { get; set; }
}

public class CreateAssetRequest
{
    public string name { get; set; } = string.Empty;
    public string? description { get; set; } = string.Empty;
    public Type assetTypeId { get; set; }
    public Genre genre { get; set; }
    public bool isForSale { get; set; }
    public bool isLimited { get; set; }
    public bool isLimitedUnique { get; set; }
    public int? price { get; set; }
    public int? maxCopies { get; set; }
    public DateTime? offsaleDeadline { get; set; }
    public long? robloxAssetId { get; set; }
    public IFormFile? rbxm { get; set; }
    public string? packageAssetIds { get; set; }
}

public class CreateClothingRequest
{
    public string name { get; set; } = string.Empty;
    public string? description { get; set; }
    public Type assetTypeId { get; set; }
    public Genre genre { get; set; }
    public bool isForSale { get; set; }
    public int? price { get; set; }
    public long? robloxAssetId { get; set; }
    public IFormFile? file { get; set; }
}

public class MigrateItemRequest
{
    public long assetId { get; set; }
    public bool isForSale { get; set; }
    public int? price { get; set; }
}

public class MigrateItemAlternateRequest
{
    public string url { get; set; } = string.Empty;
    public bool disableRender { get; set; }
}

public class MigrateItemResponse
{
    public long assetId { get; set; }
    public long assetVersionId { get; set; }
}

public class CreateAssetVersionRequest
{
    public long assetId { get; set; }
    public IFormFile? rbxm { get; set; }
}

public class DateTimeSerialized
{
    public DateTime clock { get; set; }
}

public class CopyAssetRequest
{
    public long assetId { get; set; }
    public bool force { get; set; }
}

public class AssetVersionWithIdEntry
{
    public long assetId { get; set; }
}

public class RefundTransactionEntry
{
    public long id { get; set; }
    public CurrencyType currencyType { get; set; }
    public long amount { get; set; }
    public long? userAssetId { get; set; }
    public long userId { get; set; }
    public long otherUserId { get; set; }
    public long assetId { get; set; }
}
