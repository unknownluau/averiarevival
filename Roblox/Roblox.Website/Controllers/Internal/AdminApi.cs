using DSharpPlus;
using Microsoft.AspNetCore.Mvc;
using Roblox.Dto;
using Roblox.Dto.AbuseReport;
using Roblox.Dto.Admin;
using Roblox.Dto.Assets;
using Roblox.Dto.Economy;
using Roblox.Dto.Groups;
using Roblox.Dto.Staff;
using Roblox.Dto.Users;
using Roblox.Exceptions;
using Roblox.Models.AbuseReport;
using Roblox.Models.Assets;
using Roblox.Models.Db;
using Roblox.Models.Economy;
using Roblox.Models.Sessions;
using Roblox.Models.Staff;
using Roblox.Models.Trades;
using Roblox.Models.Users;
using Roblox.Services;
using Roblox.Services.App.FeatureFlags;
using Roblox.Services.Exceptions;
using Roblox.Website.Filters;
using Roblox.Website.WebsiteModels.Asset;
using System.ComponentModel.DataAnnotations;
using System.Text;
using Exception = System.Exception;
using Type = Roblox.Models.Assets.Type;
// just to shut the compiler up
#pragma warning disable CS8604
// ReSharper disable InconsistentNaming

namespace Roblox.Website.Controllers;

[ApiController]
[NonController]
[Route("/admin-api/api/")]
[AdminTwoFactorFilter]
//[AdminTwoFactorFilter]
#if RELEASE
[ApiExplorerSettings(IgnoreApi = true)]
#endif
public class AdminApiController : ControllerBase
{
    private bool IsLoggedIn()
    {
        return base.userSession != null;
    }

    private new UserSession userSession
    {
        get
        {
            if (base.userSession == null)
                throw new StaffException("Not logged in");
            return base.userSession!;
        }
    }

    private async Task<AdminActorContext> GetActorContext()
    {
        var session = userSession;
        var isOwner = StaffFilter.IsOwner(session.userId);
        return new AdminActorContext
        {
            userId = session.userId,
            sessionId = session.sessionId,
            isOwner = isOwner,
            permissions = isOwner ? Enum.GetValues<Access>() : (await StaffFilter.GetPermissions(session.userId)).ToArray(),
        };
    }

    [HttpGet("2fa")]
    [SkipAdminTwoFactor]
    public async Task<IActionResult> ShowPrompt([FromQuery] string? returnUrl)
    {
        if (string.IsNullOrEmpty(returnUrl)) returnUrl = "/admin/";

        var returnUrlJson = System.Text.Json.JsonSerializer.Serialize(returnUrl);

        return Content($$"""
        <script>
            var code = prompt("Enter your 2FA code");
            if (code) {
                fetch(`/admin-api/api/2fa/verify?code=${code}`, {
                    method: "POST",
                }).then(r => {
                    if (r.ok) window.location = {{returnUrlJson}};
                    else prompt("Invalid code, try again") && (window.location = window.location.href);
                });
            } else {
                window.location = "/home";
            }
        </script>
    """, "text/html");
    }

    [HttpPostBypass("2fa/verify")]
    [SkipAdminTwoFactor]
    public async Task<IActionResult> VerifyPrompt([FromQuery] string code)
    {
        if (!IsLoggedIn())
            throw new Roblox.Services.Exceptions.RobloxException(401, 0, "Unauthorized");

        var session = safeUserSession;
        if (!await IsStaff(session.userId))
            throw new Roblox.Services.Exceptions.RobloxException(Roblox.Services.Exceptions.RobloxException.Forbidden, 0, "Forbidden");

        await services.adminApi.ValidateTwoFactorCodeAsync(session.userId, session.sessionId, code);
        await AdminTwoFactorFilter.MarkVerified(session.userId, session.sessionId);
        return Ok();
    }

    [HttpGet("permissions")]
    public async Task<AdminPermissionsResponse> GetPermissions()
    {
        return await services.adminApi.GetPermissionsAsync(await GetActorContext());
    }

    [HttpGet("staff/list"), StaffFilter(Access.SetPermissions)]
    public async Task<IEnumerable<UserId>> GetAllStaff()
    {
        return await services.adminApi.GetAllStaffAsync();
    }


    [HttpGet("staff/permissions/list"), StaffFilter(Access.SetPermissions)]
    public IEnumerable<Access> GetAllPermissions()
    {
        return services.adminApi.GetAllPermissions();
    }

    [HttpGet("staff/permissions"), StaffFilter(Access.SetPermissions)]
    public async Task<IEnumerable<StaffUserPermissionEntry>> GetUserPermissions(long userId)
    {
        return await services.adminApi.GetUserPermissionsAsync(userId);
    }

    [HttpPost("staff/permissions"), StaffFilter(Access.SetPermissions)]
    public async Task SetUserPermissions(long userId, Access permission)
    {
        await services.adminApi.SetUserPermissionsAsync(userId, permission, await GetActorContext());
    }

    [HttpDelete("staff/permissions"), StaffFilter(Access.SetPermissions)]
    public async Task DeletePermission(long userId, Access permission)
    {
        await services.adminApi.DeletePermissionAsync(userId, permission);
    }

    [HttpGet("stats"), StaffFilter(Access.GetStats)]
    public AdminStatsResponse GetStatus()
    {
        return services.adminApi.GetStatus();
    }

    [HttpGet("crash"), StaffFilter(Access.GetStats)]
    public Task CrashSite()
    {
        services.adminApi.CrashSite(new AdminActorContext
        {
            userId = safeUserSession.userId,
            sessionId = safeUserSession.sessionId,
            isOwner = StaffFilter.IsOwner(safeUserSession.userId),
        });
        return Task.CompletedTask;
    }

    [HttpGet("alert"), StaffFilter(Access.GetAlert)]
    public async Task<AdminSystemMessageResponse> GetSystemMessage()
    {
        return await services.adminApi.GetSystemMessageAsync();
    }

    [HttpPost("alert"), StaffFilter(Access.SetAlert)]
    public async Task SetAlert([Required, FromBody] SetAlertRequest request)
    {
        await services.adminApi.SetAlertAsync(request, await GetActorContext());
    }

    [HttpPost("create-user"), StaffFilter(Access.CreateUser)]
    public async Task<UserId> CreateUser([Required, FromBody] CreateUserRequest req)
    {
        return await services.adminApi.CreateUserAsync(req);
    }

    [HttpPost("force-application"), StaffFilter(Access.ForceApplication)]
    public async Task<AdminMessageResponse> ForceApplication([Required, FromBody] ForceApplicationReq req)
    {
        return await services.adminApi.ForceApplicationAsync(req);
    }
    
    [HttpGet("groups/pending-icons"), StaffFilter(Access.GetPendingGroupIcons)]
    [SkipAdminTwoFactor]
    public async Task<IEnumerable<PendingGroupIconEntry>> GetPendingIcons()
    {
        return await services.adminApi.GetPendingIconsAsync();
    }

    [HttpPost("gift-users"),  StaffFilter(Access.CreateAsset)]
    public async Task<IActionResult> GiftUsers([FromBody] GiftUsersRequest req)
    {
        await services.adminApi.GiftUsersAsync(req, userSession.userId, StaffFilter.IsOwner(userSession.userId));
        return Ok();
    }

    [HttpGet("asset/moderation-details"), StaffFilter(Access.GetAssetModerationDetails)]
    public async Task<PendingAssetEntry> GetModerationDetails(long assetId)
    {
        return await services.adminApi.GetModerationDetailsAsync(assetId, StaffFilter.IsOwner);
    }

    [HttpGet("assets/get-asset-stream"), StaffFilter(Access.GetPendingModerationItems)]
    public async Task<IActionResult> GetPendingAssetStream(long assetId)
    {
        var content = await services.adminApi.GetPendingAssetStreamAsync(assetId, userSession.userId, StaffFilter.IsOwner(userSession.userId));
        return File(content, "application/octet-stream");
    }

    [HttpGet("assets/pending-assets"), StaffFilter(Access.GetPendingModerationItems)]
    [SkipAdminTwoFactor]
    public async Task<IEnumerable<PendingAssetEntry>> GetPendingAssets()
    {
        return await services.adminApi.GetPendingAssetsAsync(userSession.userId, StaffFilter.IsOwner(userSession.userId), StaffFilter.IsOwner);
    }

    [HttpPost("asset/moderate"), StaffFilter(Access.SetAssetModerationStatus)]
    public async Task ModerateAsset([Required, FromBody] ModerateAssetRequest request)
    {
        await services.adminApi.ModerateAssetAsync(request, safeUserSession.userId, StaffFilter.IsOwner(safeUserSession.userId), StaffFilter.IsOwner);
    }

    [HttpPost("asset/moderate-and-delete"), StaffFilter(Access.SetAssetModerationStatus)]
    public async Task ModerateAndDeleteItem([Required, FromBody] ModerateAssetRequest request)
    {
        await services.adminApi.ModerateAndDeleteItemAsync(request, safeUserSession.userId, StaffFilter.IsOwner(safeUserSession.userId), StaffFilter.IsOwner);
    }

    [HttpGet("icons/pending-assets"), StaffFilter(Access.GetPendingModerationGameIcons)]
    [SkipAdminTwoFactor]
    public async Task<IEnumerable<PendingAssetIconEntry>> GetPendingAssetIcons()
    {
        return await services.adminApi.GetPendingAssetIconsAsync();
    }

    [HttpPost("icon/moderate"), StaffFilter(Access.SetGameIconModerationStatus)]
    public async Task ModerateIcon([Required, FromBody] ModerateIconRequest request)
    {
        await services.adminApi.ModerateIconAsync(request, StaffFilter.IsOwner(userSession.userId));
    }

    [HttpPost("groups/icon-toggle"), StaffFilter(Access.SetGroupIconModerationStatus)]
    public async Task ToggleIcon([Required, FromBody] IconToggleRequest request)
    {
        await services.adminApi.ToggleGroupIconAsync(request, userSession.userId);
    }

    [HttpGet("groups/get-by-id"), StaffFilter(Access.GetGroupManageInfo)]
    public async Task<AdminGroupModerationInfoResponse> GetGroupModerationInfo(long groupId)
    {
        return await services.adminApi.GetGroupModerationInfoAsync(groupId);
    }

    [HttpGet("user-joins"), StaffFilter(Access.GetUserJoinCount)]
    public async Task<AdminTotalResponse> GetUserJoinCount(string period)
    {
        return await services.adminApi.GetUserJoinCountAsync(period);
    }

    [HttpGet("users"), StaffFilter(Access.GetUsersList)]
    public async Task<AdminUsersResponse> GetUsers(string orderByColumn = "user.id", string? orderByMode = "asc", int limit = 10,
        int offset = 0, string? query = null, long? userId = null)
    {
        return await services.adminApi.GetUsersAsync(orderByColumn, orderByMode, limit, offset, query, userId);
    }

    [HttpGet("user/search-by-mac"), StaffFilter(Access.ViewMacAddresses)]
    public async Task<IReadOnlyCollection<AdminIdentitySearchEntry>> SearchUsersByMacAddress(
        [Required, FromQuery] string macAddress, bool exactSetOnly = false)
    {
        return await services.adminApi.SearchUsersByMacAddressAsync(await GetActorContext(), macAddress, exactSetOnly);
    }

    [HttpGet("user/search-by-ip"), StaffFilter(Access.ViewMacAddresses)]
    public async Task<IReadOnlyCollection<AdminIdentitySearchEntry>> SearchUsersByIpHash(
        [Required, FromQuery] string ipHash)
    {
        return await services.adminApi.SearchUsersByIpHashAsync(await GetActorContext(), ipHash);
    }

    [HttpGet("ip-ban/status"), StaffFilter(Access.BanUser)]
    public async Task<AdminIpBanStatusResponse> GetIpBanStatus([Required, FromQuery] string ipHash)
    {
        return await services.adminApi.GetIpBanStatusAsync(await GetActorContext(), ipHash);
    }

    [HttpPost("ip-ban"), StaffFilter(Access.BanUser)]
    public async Task SetIpBan([Required, FromBody] AdminIpBanRequest request)
    {
        await services.adminApi.SetIpBanAsync(await GetActorContext(), request);
    }

    [HttpDelete("ip-ban"), StaffFilter(Access.BanUser)]
    public async Task RevokeIpBan([Required, FromQuery] string ipHash)
    {
        await services.adminApi.RevokeIpBanAsync(await GetActorContext(), ipHash);
    }

    [HttpGet("user"), StaffFilter(Access.GetUserDetailed)]
    public async Task<AdminDataRow> GetUserInfoDetailed(long userId)
    {
        return await services.adminApi.GetUserInfoDetailedAsync(userId, StaffFilter.IsOwner);
    }

    private bool IsAdmin()
    {
        return StaffFilter.IsOwner(userSession.userId);
    }

    private async Task<bool> IsStaff(long userId)
    {
        return StaffFilter.IsOwner(userId) || (await StaffFilter.GetPermissions(userId)).Any();
    }

    [HttpPost("unban"), StaffFilter(Access.UnbanUser)]
    public async Task UnbanUser([Required, FromBody] UserIdRequest request)
    {
        await services.adminApi.UnbanUserAsync(request, await GetActorContext());
    }

    [HttpPost("ban"), StaffFilter(Access.BanUser)]
    public async Task BanUser([Required, FromBody] BanUserRequest request)
    {
        await services.adminApi.BanUserAsync(request, await GetActorContext(), StaffFilter.IsOwner);
    }

    [HttpPost("user/create-message"), StaffFilter(Access.CreateMessage)]
    public async Task CreateMessage([Required, FromBody] CreateMessageRequest request)
    {
        await services.adminApi.CreateMessageAsync(request, await GetActorContext());
    }

    [HttpGet("user/messages-from-admins"), StaffFilter(Access.GetAdminMessages)]
    public async Task<IReadOnlyCollection<AdminDataRow>> GetMessagesFromStaff(long userId, int limit = 10, int offset = 0)
    {
        return await services.adminApi.GetMessagesFromStaffAsync(userId, limit, offset);
    }

    [HttpPost("user/nullify-password"), StaffFilter(Access.NullifyPassword)]
    public async Task NullifyUserPassword([Required, FromBody] UserIdRequest request)
    {
        await services.adminApi.NullifyUserPasswordAsync(request, await GetActorContext(), StaffFilter.IsOwner);
    }

    [HttpPost("user/logout"), StaffFilter(Access.DestroyAllSessionsForUser)]
    public async Task DeleteAllSessions([Required, FromBody] UserIdRequest request)
    {
        await services.adminApi.DeleteAllSessionsAsync(request);
    }

    [HttpPost("user/lock"), StaffFilter(Access.LockAccount)]
    public async Task LockUser([Required, FromBody] UserIdRequest request)
    {
        await services.adminApi.LockUserAsync(request, await GetActorContext(), StaffFilter.IsOwner);
    }

    [HttpPost("user/regenerate-avatar"), StaffFilter(Access.RegenerateAvatar)]
    public async Task RegenAvatarRequest([Required, FromBody] UserIdRequest request)
    {
        await services.adminApi.RegenerateAvatarAsync(request);
    }

    [HttpPost("user/reset-avatar"), StaffFilter(Access.ResetAvatar)]
    public async Task ResetAvatar([Required, FromBody] UserIdRequest request)
    {
        await services.adminApi.ResetAvatarAsync(request, StaffFilter.IsOwner);
    }
    
    [HttpGet("user/mac-address-history"), StaffFilter(Access.ViewMacAddresses)]
    public async Task<IReadOnlyCollection<AdminMacAddressHistoryEntry>> GetMacAddressHistory([Required, FromQuery] long userId)
    {
        return await services.adminApi.GetMacAddressHistoryAsync(userId, await GetActorContext());
    }

    [HttpGet("alt-accounts/by-mac"), StaffFilter(Access.ViewMacAddresses)]
    public async Task<IReadOnlyCollection<AdminAltAccountByMacEntry>> GetAltAccountsByMac(int limit = 50, int offset = 0)
    {
        return await services.adminApi.GetAltAccountsByMacAsync(await GetActorContext(), limit, offset);
    }

    [HttpGet("user/alt-accounts"), StaffFilter(Access.ViewMacAddresses)]
    public async Task<AdminUserAltAccountsResponse> GetUserAltAccounts([Required, FromQuery] long? userId)
    {
        return await services.adminApi.GetUserAltAccountScoresAsync(await GetActorContext(), userId!.Value);
    }

    [HttpGet("user/ban-history"), StaffFilter(Access.BanUser)]
    public async Task<IReadOnlyCollection<AdminUserBanHistoryEntry>> GetUserBanHistory([Required, FromQuery] long userId)
    {
        return await services.adminApi.GetUserBanHistoryAsync(userId);
    }

    [HttpGet("user/status-history"), StaffFilter(Access.GetUserStatusHistory)]
    public async Task<IReadOnlyCollection<AdminDataRow>> GetUserStatusHistory([Required, FromQuery] long userId)
    {
        return await services.adminApi.GetUserStatusHistoryAsync(userId);
    }

    [HttpGet("user/comment-history"), StaffFilter(Access.DeleteComment)]
    public async Task<IReadOnlyCollection<AdminDataRow>> GetUserCommentHistory([Required, FromQuery] long userId)
    {
        return await services.adminApi.GetUserCommentHistoryAsync(userId);
    }

    [HttpDelete("user/status"), StaffFilter(Access.DeleteUserStatus)]
    public async Task DeleteUserStatus([Required, FromQuery] long userId, [Required, FromQuery] long statusId)
    {
        await services.adminApi.DeleteUserStatusAsync(userId, statusId);
    }

    [HttpPost("asset/refund-transaction"), StaffFilter(Access.RefundAndDeleteFirstPartyAssetSale)]
    public async Task RefundTransaction(long transactionId, long assetId, long expectedAmount, long userId)
    {
        await services.adminApi.RefundTransactionAsync(transactionId, assetId, expectedAmount, userId, await GetActorContext());
    }

    [HttpGet("asset/product-history"), StaffFilter(Access.GetSaleHistoryForAsset)]
    public async Task<IReadOnlyCollection<AdminDataRow>> GetAssetProductHistory(long assetId)
    {
        return await services.adminApi.GetAssetProductHistoryAsync(assetId);
    }

    [HttpGet("asset/sale-history"), StaffFilter(Access.GetSaleHistoryForAsset)]
    public async Task<IReadOnlyCollection<AdminDataRow>> GetSaleHistory(long assetId, int limit, int offset, DateTime? start = null, DateTime? end = null)
    {
        return await services.adminApi.GetSaleHistoryAsync(assetId, limit, offset, start, end);
    }

    [HttpGet("logs"), StaffFilter(Access.GetAdminLogs)]
    public async Task<AdminModerationLogsResponse> GetModerationLogs(string logType, int limit = 10, int offset = 0, bool descending = true, string? author = null, string? actioned = null)
    {
        return await services.adminApi.GetModerationLogsAsync(logType, limit, offset, descending, author, actioned);
    }
    
    [HttpGet("getbadges"), StaffFilter(Access.GetUserBadges)]
    public async Task<IEnumerable<Roblox.Dto.Users.BadgeEntry>> GetUserBadges(long userId)
    {
        return await services.adminApi.GetUserBadgesAsync(userId);
    }

    [HttpPost("givebadge"), StaffFilter(Access.GiveUserBadge)]
    public async Task GiveUserBadge([Required, FromBody] GiveBadgeRequest request)
    {
        await services.adminApi.GiveUserBadgeAsync(request, await GetActorContext(), StaffFilter.IsOwner);
    }

    [HttpPost("deletebadge"), StaffFilter(Access.DeleteUserBadge)]
    public async Task DeleteUserBadge([Required, FromBody] GiveBadgeRequest request)
    {
        await services.adminApi.DeleteUserBadgeAsync(request, await GetActorContext(), StaffFilter.IsOwner);
    }

    [HttpPost("givetickets"), StaffFilter(Access.GiveUserRobux)]
    public async Task GiveUserTickets([Required, FromBody] GiveUserTicketsRequest request)
    {
        await services.adminApi.GiveUserTicketsAsync(request, await GetActorContext());
    }

    [HttpPost("giverobux"), StaffFilter(Access.GiveUserRobux)]
    public async Task GiveUserRobux([Required, FromBody] GiveUserRobuxRequest request)
    {
        await services.adminApi.GiveUserRobuxAsync(request, await GetActorContext());
    }

    [HttpGet("user-collectibles"), StaffFilter(Access.GetUserCollectibles)]
    public async Task<IReadOnlyCollection<AdminDataRow>> GetUserCollectibles(long userId)
    {
        return await services.adminApi.GetUserCollectiblesAsync(userId);
    }

    [HttpPost("removeitem"), StaffFilter(Access.RemoveUserItem)]
    public async Task RemoveItem([Required, FromBody] RemoveItemRequest request)
    {
        await services.adminApi.RemoveItemAsync(request, await GetActorContext());
    }

    [HttpGet("assets/giveitem-circ"), StaffFilter(Access.GiveUserItem)]
    public async Task<IEnumerable<StaffUserAssetTrackEntry>> GetGiveItemCirc(long assetId, int limit)
    {
        return await services.adminApi.GetGiveItemCircAsync(assetId, limit);
    }

    [HttpPost("giveitem"), StaffFilter(Access.GiveUserItem)]
    public async Task GiveItem([Required, FromBody] GiveItemRequest request)
    {
        await services.adminApi.GiveItemAsync(request, await GetActorContext());
    }

    [HttpGet("trackitem"), StaffFilter(Access.TrackItem)]
    public async Task<IReadOnlyCollection<AdminTrackedItemHistoryEntry>> TrackItem(long userAssetId)
    {
        return await services.adminApi.TrackItemAsync(userAssetId);
    }

    [HttpPost("user/delete"), StaffFilter(Access.DeleteUser)]
    public async Task DeleteUser([Required, FromBody] UserIdRequest request)
    {
        await services.adminApi.DeleteUserAsync(request, StaffFilter.IsOwner);
    }

    [HttpGet("user/usernames"), StaffFilter(Access.GetPreviousUsernames)]
    public async Task<IEnumerable<string>> GetPreviousUsernames(long userId)
    {
        return await services.adminApi.GetPreviousUsernamesAsync(userId);
    }

    [HttpPost("user/usernames/delete"), StaffFilter(Access.DeleteUsername)]
    public async Task DeleteUsername([Required, FromBody] DeleteUsernameRequest request)
    {
        await services.adminApi.DeleteUsernameAsync(request, await GetActorContext(), StaffFilter.IsOwner);
    }

    [HttpDelete("user/comment"), StaffFilter(Access.DeleteComment)]
    public async Task DeleteComment([Required, FromQuery] long userId, [Required, FromQuery] long commentId)
    {
        await services.adminApi.DeleteCommentAsync(userId, commentId);
    }

    [HttpPost("delete-forum-post"), StaffFilter(Access.DeleteForumPost)]
    public async Task DeleteForumPost([Required, FromBody] DeleteForumPostRequest request)
    {
        await services.adminApi.DeleteForumPostAsync(request);
    }

    [HttpPost("lock-forum-thread"), StaffFilter(Access.LockForumThread)]
    public async Task LockForumThread(long threadId)
    {
        await services.adminApi.LockForumThreadAsync(threadId);
    }

    [HttpPost("lottery/run"), StaffFilter(Access.RunLottery)]
    public async Task<AdminLotteryRunResponse> RunLottery()
    {
        return await services.adminApi.RunLotteryAsync(await GetActorContext());
    }

    [HttpGet("lottery/get-users-eligible")]
    public async Task<IEnumerable<UserLotteryEntry>> GetEligibleLotteryUsers()
    {
        return await services.adminApi.GetEligibleLotteryUsersAsync();
    }

    [HttpGet("lottery/get-items")]
    public async Task<IEnumerable<LotteryItemEntry>> GetLotteryItems()
    {
        return await services.adminApi.GetLotteryItemsAsync();
    }


    [HttpGet("asset/types")]
    public Dictionary<int,string> GetAssetTypes()
    {
        return services.adminApi.GetAssetTypes();
    }

    [HttpGet("asset/genres")]
    public Dictionary<int,string> GetAssetGenres()
    {
        return services.adminApi.GetAssetGenres();
    }

    [HttpPost("asset/re-render"), StaffFilter(Access.RequestAssetReRender)]
    public async Task RequestAssetReRender([Required, FromBody] ReRenderRequest request)
    {
        await services.adminApi.RequestAssetReRenderAsync(request);
    }

    [HttpPost("asset/fix-bugged-renders"), StaffFilter(Access.RequestAssetReRender)]
    public async Task<FixBuggedRendersResponse> FixBuggedRenders([FromBody] FixBuggedRendersRequest request)
    {
        return await services.adminApi.FixBuggedRendersAsync(request);
    }

    [HttpGet("asset/details"), StaffFilter(Access.GetProductDetails)]
    public async Task<AdminAssetDetailsResponse> GetAssetDetails(long assetId)
    {
        return await services.adminApi.GetAssetDetailsAsync(assetId);
    }

    [HttpGet("product/details"), StaffFilter(Access.GetProductDetails)]
    public async Task<ProductEntry> GetProductDetails(long assetId)
    {
        return await services.adminApi.GetProductDetailsAsync(assetId);
    }

    [HttpPatch("asset/product"), StaffFilter(Access.SetAssetProduct)]
    public async Task UpdateAssetProduct([Required, FromBody] UpdateProductRequest request)
    {
        await services.adminApi.UpdateAssetProductAsync(request, await GetActorContext());
    }

    [HttpPost("asset/start-sale"), StaffFilter(Access.SetAssetProduct)]
    public async Task StartAssetSale([Required, FromBody] StartSaleRequest request)
    {
        await services.adminApi.StartAssetSaleAsync(request, await GetActorContext());
    }

    [HttpPost("asset/end-sale"), StaffFilter(Access.SetAssetProduct)]
    public async Task EndAssetSale([Required, FromBody] EndSaleRequest request)
    {
        await services.adminApi.EndAssetSaleAsync(request, await GetActorContext());
    }

    [HttpPost("bundle/copy-from-roblox"), StaffFilter(Access.CreateBundleCopiedFromRoblox)]
    public async Task<CreateResponse> CopyBundle(long bundleId)
    {
        return await services.adminApi.CopyBundleAsync(bundleId, await GetActorContext());
    }

    [HttpPost("asset/backport-from-roblox"), StaffFilter(Access.CreateAssetCopiedFromRoblox)]
    public async Task<AdminAssetIdResponse> BackportAssetFromRoblox([Required, FromBody] CopyAssetRequest request)
    {
        return await services.adminApi.BackportAssetFromRobloxAsync(request, await GetActorContext());
    }

    [HttpPost("asset/bulk-backport-from-roblox"), StaffFilter(Access.CreateAssetCopiedFromRoblox)]
    public async Task<BulkCopyAssetResponse> BackportAssetsFromRoblox([Required, FromBody] BulkCopyAssetRequest request)
    {
        return await services.adminApi.BackportAssetsFromRobloxAsync(request, await GetActorContext());
    }

    [HttpPost("asset/copy-from-roblox"), StaffFilter(Access.CreateAssetCopiedFromRoblox)]
    public async Task<AdminAssetIdResponse> CopyAssetFromRoblox([Required, FromBody] CopyAssetRequest request)
    {
        return await services.adminApi.CopyAssetFromRobloxAsync(request, await GetActorContext());
    }

    [HttpPost("asset/bulk-copy-from-roblox"), StaffFilter(Access.CreateAssetCopiedFromRoblox)]
    public async Task<BulkCopyAssetResponse> CopyAssetsFromRoblox([Required, FromBody] BulkCopyAssetRequest request)
    {
        return await services.adminApi.CopyAssetsFromRobloxAsync(request, await GetActorContext());
    }

    [HttpGet("ugc-requests/pending"), StaffFilter(Access.PendingUgcItems)]
    public async Task<IEnumerable<PendingUgcRequestEntry>> GetPendingUgcRequests()
    {
        return await services.adminApi.GetPendingUgcRequestsAsync();
    }

    [HttpPost("ugc-request/moderate"), StaffFilter(Access.PendingUgcItems)]
    public async Task<AdminSuccessResponse> ModerateUgcRequest([Required, FromBody] ModerateUgcRequestBody request)
    {
        return await services.adminApi.ModerateUgcRequestAsync(request, await GetActorContext());
    }

    [HttpPost("asset/create"), StaffFilter(Access.CreateAsset)]
    public async Task<CreateResponse> CreateAsset([Required, FromForm] CreateAssetRequest request)
    {
        return await services.adminApi.CreateAssetAsync(request);
    }

    [HttpPost("asset/create/clothing"), StaffFilter(Access.CreateClothingAsset)]
    public async Task<CreateResponse> CreateClothingAsset([Required, FromForm] CreateClothingRequest request)
    {
        return await services.adminApi.CreateClothingAssetAsync(request);
    }

    [HttpPost("asset/create/from-roblox"), StaffFilter(Access.MigrateAssetFromRoblox)]
    public async Task<MigrateItem> CopyAnyItemFromRoblox([Required, FromBody] MigrateItemAlternateRequest request)
    {
        return await MigrateItem.MigrateItemFromRoblox(request.url);
    }

    [HttpGet("group-verify"), StaffFilter(Access.LockAndUnlockGroup)]
    public async Task<AdminMessageResponse> GroupVerify(long groupId, bool verify)
    {
        return await services.adminApi.GroupVerifyAsync(groupId, verify, await GetActorContext());
    }   

    [HttpGet("create-promocode"), StaffFilter(Access.GiveUserItem)]
    public async Task<AdminMessageResponse> CreatePromocode(string promocode, int? robux, long? assetId)
    {
        return await services.adminApi.CreatePromocodeAsync(promocode, robux, assetId, await GetActorContext());
    }

    [HttpGet("delete-promocode"), StaffFilter(Access.GiveUserItem)]
    public async Task<AdminMessageResponse> DeletePromocode(string promocode)
    {
        return await services.adminApi.DeletePromocodeAsync(promocode, await GetActorContext());
    }

    [HttpPost("create-game"), StaffFilter(Access.CreateGameForUser)]
    public async Task<AdminCreateGameResponse> CreateGame([Required, FromBody] UserIdRequest request)
    {
        return await services.adminApi.CreateGameAsync(request);
    }

    [HttpPost("asset/version/create"), StaffFilter(Access.CreateAssetVersion)]
    public async Task<AssetVersionWithIdEntry> CreateAssetVersion([Required, FromForm] CreateAssetVersionRequest request)
    {
        return await services.adminApi.CreateAssetVersionAsync(request, await GetActorContext());
    }

    [HttpPost("infrastructure/request-update"), StaffFilter(Access.RequestWebsiteUpdate)]
    public AdminMessageResponse RequestUpdate()
    {
        throw new StaffException("Feature has been removed");
    }

    [HttpGet("feature-flags/all"), StaffFilter(Access.ManageFeatureFlags)]
    public IReadOnlyDictionary<FeatureFlag, bool> GetAllFlags()
    {
        return services.adminApi.GetAllFlags();
    }

    [HttpPost("feature-flags/enable"), StaffFilter(Access.ManageFeatureFlags)]
    public async Task EnableFlag(string featureFlag)
    {
        await services.adminApi.EnableFlagAsync(featureFlag);
    }

    [HttpPost("feature-flags/disable"), StaffFilter(Access.ManageFeatureFlags)]
    public async Task DisableFlag(string featureFlag)
    {
        await services.adminApi.DisableFlagAsync(featureFlag);
    }

    [HttpGet("players/in-game"), StaffFilter(Access.GetUsersInGame)]
    public async Task<IReadOnlyCollection<AdminDataRow>> GetInGamePlayers()
    {
        return await services.adminApi.GetInGamePlayersAsync();
    }

    [HttpGet("players/online-count"), StaffFilter(Access.GetUsersOnline)]
    public async Task<AdminTotalResponse> GetOnlinePlayersCount()
    {
        return await services.adminApi.GetOnlinePlayersCountAsync();
    }



    [HttpGet("users/{userId:long}/transactions"), StaffFilter(Access.GetUserTransactions)]
    public async Task<IEnumerable<TransactionEntryDb>> GetUserTransactions(long userId, PurchaseType type, int offset, int limit)
    {
        return await services.adminApi.GetUserTransactionsAsync(userId, type, offset, limit);
    }

    [HttpGet("users/{userId:long}/all-transactions"), StaffFilter(Access.GetUserTransactions)]
    public async Task<IEnumerable<TransactionEntryDb>> GetAllUserTransactions(long userId, int offset, int limit)
    {
        return await services.adminApi.GetAllUserTransactionsAsync(userId, offset, limit);
    }

    [HttpGet("users/{userId:long}/trades"), StaffFilter(Access.GetUserTransactions)]
    public async Task<IReadOnlyCollection<AdminTradeHistoryResponse>> GetUserTrades(long userId, TradeType type, int offset, int limit)
    {
        return await services.adminApi.GetUserTradesAsync(userId, type, offset, limit);
    }

    [HttpPost("trades/{tradeId:long}/rollback"), StaffFilter(Access.RollbackTrade)]
    public async Task RollbackTrade(long tradeId)
    {
        await services.adminApi.RollbackTradeAsync(tradeId, await GetActorContext());
    }

    [HttpPost("users/{userId:long}/reset-description"), StaffFilter(Access.ResetDescription)]
    public async Task ResetDescription(long userId)
    {
        await services.adminApi.ResetDescriptionAsync(userId);
    }

    [HttpPost("users/{userId:long}/reset-username"), StaffFilter(Access.ResetUsername)]
    public async Task ResetUsername(long userId)
    {
        await services.adminApi.ResetUsernameAsync(userId, await GetActorContext(), StaffFilter.IsOwner);
    }

    [HttpPost("users/{userId:long}/verify-user")]
    public async Task VerifyUser(long userId)
    {
        await services.adminApi.VerifyUserAsync(userId, await GetActorContext());
    }

    [HttpPost("users/{userId:long}/unverify-user")]
    public async Task UnverifyUser(long userId)
    {
        await services.adminApi.UnverifyUserAsync(userId, await GetActorContext());
    }


    [HttpGet("applications/update-lock"), StaffFilter(Access.ManageApplications)]
    public async Task UpdateLocks(string ids)
    {
        await services.adminApi.UpdateLocksAsync(ids, await GetActorContext());
    }

    [HttpGet("applications/list"), StaffFilter(Access.ManageApplications)]
    public async Task<IEnumerable<UserApplicationEntry>> GetApplications(UserApplicationStatus? status, int offset, SortOrder sortOrder, string? searchQuery = null, ApplicationSearchColumn? searchColumn = null)
    {
        return await services.adminApi.GetApplicationsAsync(status, offset, sortOrder, searchQuery, searchColumn, await GetActorContext());
    }

    [HttpGet("applications/details"), StaffFilter(Access.ManageApplications)]
    public async Task<UserApplicationEntry> GetApplicationById(string id)
    {
        return await services.adminApi.GetApplicationByIdAsync(id);
    }

    [HttpGet("applications/pending-num")]
    [SkipAdminTwoFactor]
    [StaffFilter(Access.ManageApplications)]
    public async Task<AdminCountResponse> GetNumPendingApplications()
    {
        return await services.adminApi.GetNumPendingApplicationsAsync();
    }

    [HttpPost("applications/{applicationId}/approve"), StaffFilter(Access.ManageApplications)]
    public async Task<AdminApplicationApproveResponse> ApproveApplication(string applicationId)
    {
        return await services.adminApi.ApproveApplicationAsync(applicationId, await GetActorContext());
    }

    [HttpPost("applications/{applicationId}/decline"), StaffFilter(Access.ManageApplications)]
    public async Task DeclineApplication(string applicationId, string reason)
    {
        await services.adminApi.DeclineApplicationAsync(applicationId, reason, await GetActorContext());
    }

    [HttpPost("applications/{applicationId}/decline-silent"), StaffFilter(Access.ManageApplications)]
    public async Task DeclineApplicationSilently(string applicationId)
    {
        await services.adminApi.DeclineApplicationSilentlyAsync(applicationId, await GetActorContext());
    }

    [HttpPost("applications/{applicationId}/clear"), StaffFilter(Access.ClearApplications)]
    public async Task ClearApplication(string applicationId)
    {
        await services.adminApi.ClearApplicationAsync(applicationId);
    }

    [HttpGet("invites/{userId:long}"), StaffFilter(Access.ManageInvites)]
    public async Task<IEnumerable<UserInviteEntry>> GetInvitesByUser(long userId)
    {
        return await services.adminApi.GetInvitesByUserAsync(userId);
    }

    [HttpGet("text-moderation/get-latest"), StaffFilter(Access.GetAllAssetComments)]
    public async Task<AdminLatestTextModerationIdsResponse> GetLatestIdsForTextMod()
    {
        return await services.adminApi.GetLatestIdsForTextModAsync();
    }

    [HttpGet("assets/comments"), StaffFilter(Access.GetAllAssetComments)]
    public async Task<IEnumerable<StaffAssetCommentEntry>> GetAllAssetComments(int limit, int offset, string? sortOrder = "asc", long? exclusiveStartId = 0)
    {
        return await services.adminApi.GetAllAssetCommentsAsync(limit, offset, sortOrder, exclusiveStartId);
    }

    [HttpGet("groups/wall"), StaffFilter(Access.GetGroupWall)]
    public async Task<IEnumerable<StaffWallEntry>> GetAllWallPosts(int limit, int offset, string? sortOrder = "asc", long? exclusiveStartId = null)
    {
        return await services.adminApi.GetAllWallPostsAsync(limit, offset, sortOrder, exclusiveStartId);
    }

    [HttpPost("groups/wall/remove"), StaffFilter(Access.DeleteGroupWallPost)]
    public async Task RemoveWallPost(long id)
    {
        await services.adminApi.RemoveWallPostAsync(id);
    }

    [HttpGet("groups/status"), StaffFilter(Access.GetGroupStatus)]
    public async Task<IEnumerable<GroupWallPostStaff>> GetGroupStatuses(int offset, int limit, string? sortOrder = "asc", long? exclusiveStartId = null)
    {
        return await services.adminApi.GetGroupStatusesAsync(offset, limit, sortOrder, exclusiveStartId);
    }

    [HttpPost("groups/status/delete"), StaffFilter(Access.DeleteGroupStatus)]
    public async Task DeleteGroupStatus(long id)
    {
        await services.adminApi.DeleteGroupStatusAsync(id);
    }

    [HttpGet("users/status"), StaffFilter(Access.GetAllUserStatuses)]
    public async Task<IEnumerable<StaffUserStatusEntry>> GetAllUserStatuses(int offset, int limit, string? sortOrder = "asc", long? exclusiveStartId = null)
    {
        return await services.adminApi.GetAllUserStatusesAsync(offset, limit, sortOrder, exclusiveStartId);
    }

    [HttpGet("groups/list"), StaffFilter(Access.GetGroupManageInfo)]
    public async Task<IReadOnlyCollection<AdminDataRow>> GetGroupList(int offset, int limit, string sortColumn, string sortOrder)
    {
        return await services.adminApi.GetGroupListAsync(offset, limit, sortColumn, sortOrder);
    }

    [HttpGet("groups/get-by-name"), StaffFilter(Access.GetGroupManageInfo)]
    public async Task<AdminGroupModerationInfoResponse> GetGroupByName(string name)
    {
        return await services.adminApi.GetGroupByNameAsync(name);
    }

    [HttpGet("groups/audit-log"), StaffFilter(Access.GetGroupManageInfo)]
    public async Task<IReadOnlyCollection<AdminDataRow>> GetEntireAuditLog(long groupId)
    {
        return await services.adminApi.GetEntireAuditLogAsync(groupId);
    }

    [HttpPost("groups/toggle-lock-status"), StaffFilter(Access.LockAndUnlockGroup)]
    public async Task ToggleGroupLockStatus(long groupId, bool locked)
    {
        await services.adminApi.ToggleGroupLockStatusAsync(groupId, locked);
    }

    [HttpPost("groups/reset"), StaffFilter(Access.ResetGroup)]
    public async Task ResetGroup(long groupId)
    {
        await services.adminApi.ResetGroupAsync(groupId);
    }

    [HttpGet("games/play-history"), StaffFilter(Access.GetUsersInGame)]
    public async Task<IReadOnlyCollection<AdminDataRow>> GetPlayHistory(int limit, int offset)
    {
        return await services.adminApi.GetPlayHistoryAsync(limit, offset);
    }

    [HttpPost("text-moderation/request-payment"), StaffFilter(Access.GetAllAssetComments)]
    public async Task<AdminRobuxAmountResponse> RequestPayment()
    {
        return await services.adminApi.RequestPaymentAsync(await GetActorContext());
    }

    [HttpGet("chat-messages/{reportId}"), StaffFilter(Access.ManageReports)]
    public async Task<IActionResult> GetChatMessages(string reportId)
    {
        var response = await services.adminApi.GetChatMessagesAsync(reportId);
        return Content(response.content, response.contentType, Encoding.UTF8);
    }

    [HttpGet("reports/pending-count"), StaffFilter(Access.ManageReports)]
    [SkipAdminTwoFactor]
    public async Task<AdminCountResponse> GetPendingReports()
    {
        return await services.adminApi.GetPendingReportsAsync();
    }

    [HttpGet("reports/list"), StaffFilter(Access.ManageReports)]
    public async Task<IEnumerable<AbuseReportEntry>> GetReports(AbuseReportStatus status)
    {
        return await services.adminApi.GetReportsAsync(status);
    }

    [HttpPost("reports/{id}/accept"), StaffFilter(Access.ManageReports)]
    public async Task AcceptReport(string id)
    {
        await services.adminApi.AcceptReportAsync(id, await GetActorContext());
    }

    [HttpPost("reports/{id}/decline"), StaffFilter(Access.ManageReports)]
    public async Task DeclineReport(string id)
    {
        await services.adminApi.DeclineReportAsync(id, await GetActorContext());
    }

    [HttpPost("reports/{id}/invalid"), StaffFilter(Access.ManageReports)]
    public async Task DeclineReportInvalid(string id)
    {
        await services.adminApi.DeclineReportInvalidAsync(id, await GetActorContext());
    }

    [HttpGet("assets/{assetId}/owners"), StaffFilter(Access.GetAllAssetOwners)]
    public async Task<IEnumerable<CollectibleUserAssetEntry>> GetLiterallyAllOwnersKindaUnsafe(long assetId)
    {
        return await services.adminApi.GetAllOwnersAsync(assetId);
    }

    [HttpGet("moderation/get-by-thumbnail"), StaffFilter(Access.GetDetailsFromThumbnail)]
    public async Task<StaffAssetResolveThumbnailResponse> GetDetailsFromThumbnail(string url)
    {
        return await services.adminApi.GetDetailsFromThumbnailAsync(url);
    }

    /**
     **********************
     **
     ** STAFF PERFORMANCE APIS
     **
     **********************
     */
    
    [HttpGet("performance/totals/assets"), StaffFilter(Access.GetStaffPerformance)]
    public async Task<long> GetPerfTotalsAsset(long userId)
    {
        return await services.adminApi.GetPerfTotalsAssetAsync(userId);
    }
    
    [HttpGet("performance/totals/audios"), StaffFilter(Access.GetStaffPerformance)]
    public async Task<long> GetPerfTotalsAudios(long userId)
    {
        return await services.adminApi.GetPerfTotalsAudiosAsync(userId);
    }
    
    [HttpGet("performance/totals/signups"), StaffFilter(Access.GetStaffPerformance)]
    public async Task<long> GetPerfTotalsApplications(long userId)
    {
        return await services.adminApi.GetPerfTotalsApplicationsAsync(userId);
    }
    
    [HttpGet("performance/totals/reports"), StaffFilter(Access.GetStaffPerformance)]
    public async Task<long> GetPerfTotalsReports(long userId)
    {
        return await services.adminApi.GetPerfTotalsReportsAsync(userId);
    }
    
    [HttpGet("performance/totals/players-moderated"), StaffFilter(Access.GetStaffPerformance)]
    public async Task<long> GetPerfTotalsPlayersModerated(long userId)
    {
        return await services.adminApi.GetPerfTotalsPlayersModeratedAsync(userId);
    }
    
    [HttpGet("performance/permissions-gave"), StaffFilter(Access.GetStaffPerformance)]
    public async Task<AdminDateResponse> GetPerfPermDate(long userId)
    {
        return await services.adminApi.GetPerfPermDateAsync(userId);
    }
}
