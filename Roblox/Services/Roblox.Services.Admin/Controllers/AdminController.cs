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
using Roblox.Web.Infrastructure.Admin;
using Roblox.Web.Infrastructure.Controllers;
using Roblox.Web.Infrastructure.Metadata;
using Roblox.Services.Exceptions;
using System.ComponentModel.DataAnnotations;
using System.Text;
using Exception = System.Exception;
using Type = Roblox.Models.Assets.Type;
// just to shut the compiler up
#pragma warning disable CS8604
// ReSharper disable InconsistentNaming

namespace Roblox.Services.Admin.Controllers;

[ApiController]
[InternalServiceOnly]
[RequireRobloxSession] 
[RequireRobloxCsrf]
[AdminStaffFilter]
[AdminTwoFactorFilter]
[Route("/v1")]
#if RELEASE
[ApiExplorerSettings(IgnoreApi = true)]
#endif
public class AdminController : RobloxControllerBase
{
    private readonly IAdminStaffAuthorizationService _staffAuthorization;
    private readonly IAdminTwoFactorStore _twoFactorStore;

    public AdminController(IAdminStaffAuthorizationService staffAuthorization, IAdminTwoFactorStore twoFactorStore)
    {
        _staffAuthorization = staffAuthorization;
        _twoFactorStore = twoFactorStore;
    }

    private bool IsLoggedIn()
    {
        return base.userSession != null;
    }

    private new UserSession userSession
    {
        get
        {
            if (base.userSession == null)
                throw new Roblox.Services.Exceptions.RobloxException(500, 0, "Not logged in");
            return base.userSession!;
        }
    }

    private async Task<AdminActorContext> GetActorContext()
    {
        var session = userSession;
        var isOwner = _staffAuthorization.IsOwner(session.userId);
        return new AdminActorContext
        {
            userId = session.userId,
            sessionId = session.sessionId,
            isOwner = isOwner,
            permissions = isOwner ? Enum.GetValues<Access>() : (await _staffAuthorization.GetPermissionsAsync(session.userId)).ToArray(),
        };
    }

    [HttpGet("2fa")]
    [SkipAdminTwoFactor]
    public async Task<IActionResult> ShowPrompt()
    {
        var returnUrlJson = System.Text.Json.JsonSerializer.Serialize($"https://www.{Configuration.ShortBaseUrl}/admin/");

        return Content($$"""
        <script>
            var code = prompt("Enter your 2FA code");
            if (code) {
                fetch(`/v1/2fa/verify?code=${encodeURIComponent(code)}`, {
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

    [HttpPost("2fa/verify")]
    [SkipAdminTwoFactor]
    [SkipRobloxCsrf]
    public async Task<IActionResult> VerifyPrompt([FromQuery] string code)
    {
        if (!IsLoggedIn())
            throw new Roblox.Services.Exceptions.RobloxException(401, 0, "Unauthorized");

        var session = safeUserSession;
        if (!await IsStaff(session.userId))
            throw new Roblox.Services.Exceptions.RobloxException(Roblox.Services.Exceptions.RobloxException.Forbidden, 0, "Forbidden");

        await services.adminApi.ValidateTwoFactorCodeAsync(session.userId, session.sessionId, code);
        await _twoFactorStore.MarkVerifiedAsync(session.userId, session.sessionId);
        return Ok();
    }

    [HttpGet("permissions")]
    public async Task<AdminPermissionsResponse> GetPermissions()
    {
        return await services.adminApi.GetPermissionsAsync(await GetActorContext());
    }

    [HttpGet("staff/list"), AdminPermission(Access.SetPermissions)]
    public async Task<IEnumerable<UserId>> GetAllStaff()
    {
        return await services.adminApi.GetAllStaffAsync();
    }


    [HttpGet("staff/permissions/list"), AdminPermission(Access.SetPermissions)]
    public IEnumerable<Access> GetAllPermissions()
    {
        return services.adminApi.GetAllPermissions();
    }

    [HttpGet("staff/permissions"), AdminPermission(Access.SetPermissions)]
    public async Task<IEnumerable<StaffUserPermissionEntry>> GetUserPermissions(long userId)
    {
        return await services.adminApi.GetUserPermissionsAsync(userId);
    }

    [HttpPost("staff/permissions"), AdminPermission(Access.SetPermissions)]
    public async Task SetUserPermissions(long userId, Access permission)
    {
        await services.adminApi.SetUserPermissionsAsync(userId, permission, await GetActorContext());
    }

    [HttpDelete("staff/permissions"), AdminPermission(Access.SetPermissions)]
    public async Task DeletePermission(long userId, Access permission)
    {
        await services.adminApi.DeletePermissionAsync(userId, permission);
    }

    [HttpGet("stats"), AdminPermission(Access.GetStats)]
    public AdminStatsResponse GetStatus()
    {
        return services.adminApi.GetStatus();
    }

    [HttpGet("crash"), AdminPermission(Access.GetStats)]
    public Task CrashSite()
    {
        services.adminApi.CrashSite(new AdminActorContext
        {
            userId = safeUserSession.userId,
            sessionId = safeUserSession.sessionId,
            isOwner = _staffAuthorization.IsOwner(safeUserSession.userId),
        });
        return Task.CompletedTask;
    }

    [HttpGet("alert"), AdminPermission(Access.GetAlert)]
    public async Task<AdminSystemMessageResponse> GetSystemMessage()
    {
        return await services.adminApi.GetSystemMessageAsync();
    }

    [HttpPost("alert"), AdminPermission(Access.SetAlert)]
    public async Task SetAlert([Required, FromBody] SetAlertRequest request)
    {
        await services.adminApi.SetAlertAsync(request, await GetActorContext());
    }

    [HttpPost("create-user"), AdminPermission(Access.CreateUser)]
    public async Task<UserId> CreateUser([Required, FromBody] CreateUserRequest req)
    {
        return await services.adminApi.CreateUserAsync(req);
    }

    [HttpPost("force-application"), AdminPermission(Access.ForceApplication)]
    public async Task<AdminMessageResponse> ForceApplication([Required, FromBody] ForceApplicationReq req)
    {
        return await services.adminApi.ForceApplicationAsync(req);
    }
    
    [HttpGet("groups/pending-icons"), AdminPermission(Access.GetPendingGroupIcons)]
    [SkipAdminTwoFactor]
    public async Task<IEnumerable<PendingGroupIconEntry>> GetPendingIcons()
    {
        return await services.adminApi.GetPendingIconsAsync();
    }

    [HttpPost("gift-users"),  AdminPermission(Access.CreateAsset)]
    public async Task<IActionResult> GiftUsers([FromBody] GiftUsersRequest req)
    {
        await services.adminApi.GiftUsersAsync(req, userSession.userId, _staffAuthorization.IsOwner(userSession.userId));
        return Ok();
    }

    [HttpGet("asset/moderation-details"), AdminPermission(Access.GetAssetModerationDetails)]
    public async Task<PendingAssetEntry> GetModerationDetails(long assetId)
    {
        return await services.adminApi.GetModerationDetailsAsync(assetId, _staffAuthorization.IsOwner);
    }

    [HttpGet("assets/get-asset-stream"), AdminPermission(Access.GetPendingModerationItems)]
    public async Task<IActionResult> GetPendingAssetStream(long assetId)
    {
        var content = await services.adminApi.GetPendingAssetStreamAsync(assetId, userSession.userId, _staffAuthorization.IsOwner(userSession.userId));
        return File(content, "application/octet-stream");
    }

    [HttpGet("assets/pending-assets"), AdminPermission(Access.GetPendingModerationItems)]
    [SkipAdminTwoFactor]
    public async Task<IEnumerable<PendingAssetEntry>> GetPendingAssets()
    {
        return await services.adminApi.GetPendingAssetsAsync(userSession.userId, _staffAuthorization.IsOwner(userSession.userId), _staffAuthorization.IsOwner);
    }

    [HttpPost("asset/moderate"), AdminPermission(Access.SetAssetModerationStatus)]
    public async Task ModerateAsset([Required, FromBody] ModerateAssetRequest request)
    {
        await services.adminApi.ModerateAssetAsync(request, safeUserSession.userId, _staffAuthorization.IsOwner(safeUserSession.userId), _staffAuthorization.IsOwner);
    }

    [HttpPost("asset/moderate-and-delete"), AdminPermission(Access.SetAssetModerationStatus)]
    public async Task ModerateAndDeleteItem([Required, FromBody] ModerateAssetRequest request)
    {
        await services.adminApi.ModerateAndDeleteItemAsync(request, safeUserSession.userId, _staffAuthorization.IsOwner(safeUserSession.userId), _staffAuthorization.IsOwner);
    }

    [HttpGet("icons/pending-assets"), AdminPermission(Access.GetPendingModerationGameIcons)]
    [SkipAdminTwoFactor]
    public async Task<IEnumerable<PendingAssetIconEntry>> GetPendingAssetIcons()
    {
        return await services.adminApi.GetPendingAssetIconsAsync();
    }

    [HttpPost("icon/moderate"), AdminPermission(Access.SetGameIconModerationStatus)]
    public async Task ModerateIcon([Required, FromBody] ModerateIconRequest request)
    {
        await services.adminApi.ModerateIconAsync(request, _staffAuthorization.IsOwner(userSession.userId));
    }

    [HttpPost("groups/icon-toggle"), AdminPermission(Access.SetGroupIconModerationStatus)]
    public async Task ToggleIcon([Required, FromBody] IconToggleRequest request)
    {
        await services.adminApi.ToggleGroupIconAsync(request, userSession.userId);
    }

    [HttpGet("groups/get-by-id"), AdminPermission(Access.GetGroupManageInfo)]
    public async Task<AdminGroupModerationInfoResponse> GetGroupModerationInfo(long groupId)
    {
        return await services.adminApi.GetGroupModerationInfoAsync(groupId);
    }

    [HttpGet("user-joins"), AdminPermission(Access.GetUserJoinCount)]
    public async Task<AdminTotalResponse> GetUserJoinCount(string period)
    {
        return await services.adminApi.GetUserJoinCountAsync(period);
    }

    [HttpGet("users"), AdminPermission(Access.GetUsersList)]
    public async Task<AdminUsersResponse> GetUsers(string orderByColumn = "user.id", string? orderByMode = "asc", int limit = 10,
        int offset = 0, string? query = null, long? userId = null)
    {
        return await services.adminApi.GetUsersAsync(orderByColumn, orderByMode, limit, offset, query, userId);
    }

    [HttpGet("user/search-by-mac"), AdminPermission(Access.ViewMacAddresses)]
    public async Task<IReadOnlyCollection<AdminIdentitySearchEntry>> SearchUsersByMacAddress(
        [Required, FromQuery] string macAddress, bool exactSetOnly = false)
    {
        return await services.adminApi.SearchUsersByMacAddressAsync(await GetActorContext(), macAddress, exactSetOnly);
    }

    [HttpGet("user/search-by-ip"), AdminPermission(Access.ViewMacAddresses)]
    public async Task<IReadOnlyCollection<AdminIdentitySearchEntry>> SearchUsersByIpHash(
        [Required, FromQuery] string ipHash)
    {
        return await services.adminApi.SearchUsersByIpHashAsync(await GetActorContext(), ipHash);
    }

    [HttpGet("ip-ban/status"), AdminPermission(Access.BanUser)]
    public async Task<AdminIpBanStatusResponse> GetIpBanStatus([Required, FromQuery] string ipHash)
    {
        return await services.adminApi.GetIpBanStatusAsync(await GetActorContext(), ipHash);
    }

    [HttpPost("ip-ban"), AdminPermission(Access.BanUser)]
    public async Task SetIpBan([Required, FromBody] AdminIpBanRequest request)
    {
        await services.adminApi.SetIpBanAsync(await GetActorContext(), request);
    }

    [HttpDelete("ip-ban"), AdminPermission(Access.BanUser)]
    public async Task RevokeIpBan([Required, FromQuery] string ipHash)
    {
        await services.adminApi.RevokeIpBanAsync(await GetActorContext(), ipHash);
    }

    [HttpGet("user"), AdminPermission(Access.GetUserDetailed)]
    public async Task<AdminDataRow> GetUserInfoDetailed(long userId)
    {
        return await services.adminApi.GetUserInfoDetailedAsync(userId, _staffAuthorization.IsOwner);
    }

    private bool IsAdmin()
    {
        return _staffAuthorization.IsOwner(userSession.userId);
    }

    private async Task<bool> IsStaff(long userId)
    {
        return _staffAuthorization.IsOwner(userId) || (await _staffAuthorization.GetPermissionsAsync(userId)).Any();
    }

    [HttpPost("unban"), AdminPermission(Access.UnbanUser)]
    public async Task UnbanUser([Required, FromBody] UserIdRequest request)
    {
        await services.adminApi.UnbanUserAsync(request, await GetActorContext());
    }

    [HttpPost("ban"), AdminPermission(Access.BanUser)]
    public async Task BanUser([Required, FromBody] BanUserRequest request)
    {
        await services.adminApi.BanUserAsync(request, await GetActorContext(), _staffAuthorization.IsOwner);
    }

    [HttpPost("user/create-message"), AdminPermission(Access.CreateMessage)]
    public async Task CreateMessage([Required, FromBody] CreateMessageRequest request)
    {
        await services.adminApi.CreateMessageAsync(request, await GetActorContext());
    }

    [HttpGet("user/messages-from-admins"), AdminPermission(Access.GetAdminMessages)]
    public async Task<IReadOnlyCollection<AdminDataRow>> GetMessagesFromStaff(long userId, int limit = 10, int offset = 0)
    {
        return await services.adminApi.GetMessagesFromStaffAsync(userId, limit, offset);
    }

    [HttpPost("user/nullify-password"), AdminPermission(Access.NullifyPassword)]
    public async Task NullifyUserPassword([Required, FromBody] UserIdRequest request)
    {
        await services.adminApi.NullifyUserPasswordAsync(request, await GetActorContext(), _staffAuthorization.IsOwner);
    }

    [HttpPost("user/logout"), AdminPermission(Access.DestroyAllSessionsForUser)]
    public async Task DeleteAllSessions([Required, FromBody] UserIdRequest request)
    {
        await services.adminApi.DeleteAllSessionsAsync(request);
    }

    [HttpPost("user/lock"), AdminPermission(Access.LockAccount)]
    public async Task LockUser([Required, FromBody] UserIdRequest request)
    {
        await services.adminApi.LockUserAsync(request, await GetActorContext(), _staffAuthorization.IsOwner);
    }

    [HttpPost("user/regenerate-avatar"), AdminPermission(Access.RegenerateAvatar)]
    public async Task RegenAvatarRequest([Required, FromBody] UserIdRequest request)
    {
        await services.adminApi.RegenerateAvatarAsync(request);
    }

    [HttpPost("user/reset-avatar"), AdminPermission(Access.ResetAvatar)]
    public async Task ResetAvatar([Required, FromBody] UserIdRequest request)
    {
        await services.adminApi.ResetAvatarAsync(request, _staffAuthorization.IsOwner);
    }
    
    [HttpGet("user/mac-address-history"), AdminPermission(Access.ViewMacAddresses)]
    public async Task<IReadOnlyCollection<AdminMacAddressHistoryEntry>> GetMacAddressHistory([Required, FromQuery] long userId)
    {
        return await services.adminApi.GetMacAddressHistoryAsync(userId, await GetActorContext());
    }

    [HttpGet("alt-accounts/by-mac"), AdminPermission(Access.ViewMacAddresses)]
    public async Task<IReadOnlyCollection<AdminAltAccountByMacEntry>> GetAltAccountsByMac(int limit = 50, int offset = 0)
    {
        return await services.adminApi.GetAltAccountsByMacAsync(await GetActorContext(), limit, offset);
    }

    [HttpGet("user/alt-accounts"), AdminPermission(Access.ViewMacAddresses)]
    public async Task<AdminUserAltAccountsResponse> GetUserAltAccounts([Required, FromQuery] long? userId)
    {
        return await services.adminApi.GetUserAltAccountScoresAsync(await GetActorContext(), userId!.Value);
    }

    [HttpGet("user/ban-history"), AdminPermission(Access.BanUser)]
    public async Task<IReadOnlyCollection<AdminUserBanHistoryEntry>> GetUserBanHistory([Required, FromQuery] long userId)
    {
        return await services.adminApi.GetUserBanHistoryAsync(userId);
    }

    [HttpGet("user/status-history"), AdminPermission(Access.GetUserStatusHistory)]
    public async Task<IReadOnlyCollection<AdminDataRow>> GetUserStatusHistory([Required, FromQuery] long userId)
    {
        return await services.adminApi.GetUserStatusHistoryAsync(userId);
    }

    [HttpGet("user/comment-history"), AdminPermission(Access.DeleteComment)]
    public async Task<IReadOnlyCollection<AdminDataRow>> GetUserCommentHistory([Required, FromQuery] long userId)
    {
        return await services.adminApi.GetUserCommentHistoryAsync(userId);
    }

    [HttpDelete("user/status"), AdminPermission(Access.DeleteUserStatus)]
    public async Task DeleteUserStatus([Required, FromQuery] long userId, [Required, FromQuery] long statusId)
    {
        await services.adminApi.DeleteUserStatusAsync(userId, statusId);
    }

    [HttpPost("asset/refund-transaction"), AdminPermission(Access.RefundAndDeleteFirstPartyAssetSale)]
    public async Task RefundTransaction(long transactionId, long assetId, long expectedAmount, long userId)
    {
        await services.adminApi.RefundTransactionAsync(transactionId, assetId, expectedAmount, userId, await GetActorContext());
    }

    [HttpGet("asset/product-history"), AdminPermission(Access.GetSaleHistoryForAsset)]
    public async Task<IReadOnlyCollection<AdminDataRow>> GetAssetProductHistory(long assetId)
    {
        return await services.adminApi.GetAssetProductHistoryAsync(assetId);
    }

    [HttpGet("asset/sale-history"), AdminPermission(Access.GetSaleHistoryForAsset)]
    public async Task<IReadOnlyCollection<AdminDataRow>> GetSaleHistory(long assetId, int limit, int offset, DateTime? start = null, DateTime? end = null)
    {
        return await services.adminApi.GetSaleHistoryAsync(assetId, limit, offset, start, end);
    }

    [HttpGet("logs"), AdminPermission(Access.GetAdminLogs)]
    public async Task<AdminModerationLogsResponse> GetModerationLogs(string logType, int limit = 10, int offset = 0, bool descending = true, string? author = null, string? actioned = null)
    {
        return await services.adminApi.GetModerationLogsAsync(logType, limit, offset, descending, author, actioned);
    }
    
    [HttpGet("getbadges"), AdminPermission(Access.GetUserBadges)]
    public async Task<IEnumerable<Roblox.Dto.Users.BadgeEntry>> GetUserBadges(long userId)
    {
        return await services.adminApi.GetUserBadgesAsync(userId);
    }

    [HttpPost("givebadge"), AdminPermission(Access.GiveUserBadge)]
    public async Task GiveUserBadge([Required, FromBody] GiveBadgeRequest request)
    {
        await services.adminApi.GiveUserBadgeAsync(request, await GetActorContext(), _staffAuthorization.IsOwner);
    }

    [HttpPost("deletebadge"), AdminPermission(Access.DeleteUserBadge)]
    public async Task DeleteUserBadge([Required, FromBody] GiveBadgeRequest request)
    {
        await services.adminApi.DeleteUserBadgeAsync(request, await GetActorContext(), _staffAuthorization.IsOwner);
    }

    [HttpPost("givetickets"), AdminPermission(Access.GiveUserRobux)]
    public async Task GiveUserTickets([Required, FromBody] GiveUserTicketsRequest request)
    {
        await services.adminApi.GiveUserTicketsAsync(request, await GetActorContext());
    }

    [HttpPost("giverobux"), AdminPermission(Access.GiveUserRobux)]
    public async Task GiveUserRobux([Required, FromBody] GiveUserRobuxRequest request)
    {
        await services.adminApi.GiveUserRobuxAsync(request, await GetActorContext());
    }

    [HttpGet("user-collectibles"), AdminPermission(Access.GetUserCollectibles)]
    public async Task<IReadOnlyCollection<AdminDataRow>> GetUserCollectibles(long userId)
    {
        return await services.adminApi.GetUserCollectiblesAsync(userId);
    }

    [HttpPost("removeitem"), AdminPermission(Access.RemoveUserItem)]
    public async Task RemoveItem([Required, FromBody] RemoveItemRequest request)
    {
        await services.adminApi.RemoveItemAsync(request, await GetActorContext());
    }

    [HttpGet("assets/giveitem-circ"), AdminPermission(Access.GiveUserItem)]
    public async Task<IEnumerable<StaffUserAssetTrackEntry>> GetGiveItemCirc(long assetId, int limit)
    {
        return await services.adminApi.GetGiveItemCircAsync(assetId, limit);
    }

    [HttpPost("giveitem"), AdminPermission(Access.GiveUserItem)]
    public async Task GiveItem([Required, FromBody] GiveItemRequest request)
    {
        await services.adminApi.GiveItemAsync(request, await GetActorContext());
    }

    [HttpGet("trackitem"), AdminPermission(Access.TrackItem)]
    public async Task<IReadOnlyCollection<AdminTrackedItemHistoryEntry>> TrackItem(long userAssetId)
    {
        return await services.adminApi.TrackItemAsync(userAssetId);
    }

    [HttpPost("user/delete"), AdminPermission(Access.DeleteUser)]
    public async Task DeleteUser([Required, FromBody] UserIdRequest request)
    {
        await services.adminApi.DeleteUserAsync(request, _staffAuthorization.IsOwner);
    }

    [HttpGet("user/usernames"), AdminPermission(Access.GetPreviousUsernames)]
    public async Task<IEnumerable<string>> GetPreviousUsernames(long userId)
    {
        return await services.adminApi.GetPreviousUsernamesAsync(userId);
    }

    [HttpPost("user/usernames/delete"), AdminPermission(Access.DeleteUsername)]
    public async Task DeleteUsername([Required, FromBody] DeleteUsernameRequest request)
    {
        await services.adminApi.DeleteUsernameAsync(request, await GetActorContext(), _staffAuthorization.IsOwner);
    }

    [HttpDelete("user/comment"), AdminPermission(Access.DeleteComment)]
    public async Task DeleteComment([Required, FromQuery] long userId, [Required, FromQuery] long commentId)
    {
        await services.adminApi.DeleteCommentAsync(userId, commentId);
    }

    [HttpPost("delete-forum-post"), AdminPermission(Access.DeleteForumPost)]
    public async Task DeleteForumPost([Required, FromBody] DeleteForumPostRequest request)
    {
        await services.adminApi.DeleteForumPostAsync(request);
    }

    [HttpPost("lock-forum-thread"), AdminPermission(Access.LockForumThread)]
    public async Task LockForumThread(long threadId)
    {
        await services.adminApi.LockForumThreadAsync(threadId);
    }

    [HttpPost("lottery/run"), AdminPermission(Access.RunLottery)]
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

    [HttpPost("asset/re-render"), AdminPermission(Access.RequestAssetReRender)]
    public async Task RequestAssetReRender([Required, FromBody] ReRenderRequest request)
    {
        await services.adminApi.RequestAssetReRenderAsync(request);
    }

    [HttpPost("asset/fix-bugged-renders"), AdminPermission(Access.RequestAssetReRender)]
    public async Task<FixBuggedRendersResponse> FixBuggedRenders([FromBody] FixBuggedRendersRequest request)
    {
        return await services.adminApi.FixBuggedRendersAsync(request);
    }

    [HttpGet("asset/details"), AdminPermission(Access.GetProductDetails)]
    public async Task<AdminAssetDetailsResponse> GetAssetDetails(long assetId)
    {
        return await services.adminApi.GetAssetDetailsAsync(assetId);
    }

    [HttpGet("product/details"), AdminPermission(Access.GetProductDetails)]
    public async Task<ProductEntry> GetProductDetails(long assetId)
    {
        return await services.adminApi.GetProductDetailsAsync(assetId);
    }

    [HttpPatch("asset/product"), AdminPermission(Access.SetAssetProduct)]
    public async Task UpdateAssetProduct([Required, FromBody] UpdateProductRequest request)
    {
        await services.adminApi.UpdateAssetProductAsync(request, await GetActorContext());
    }

    [HttpPost("asset/start-sale"), AdminPermission(Access.SetAssetProduct)]
    public async Task StartAssetSale([Required, FromBody] StartSaleRequest request)
    {
        await services.adminApi.StartAssetSaleAsync(request, await GetActorContext());
    }

    [HttpPost("asset/end-sale"), AdminPermission(Access.SetAssetProduct)]
    public async Task EndAssetSale([Required, FromBody] EndSaleRequest request)
    {
        await services.adminApi.EndAssetSaleAsync(request, await GetActorContext());
    }

    [HttpPost("bundle/copy-from-roblox"), AdminPermission(Access.CreateBundleCopiedFromRoblox)]
    public async Task<CreateResponse> CopyBundle(long bundleId)
    {
        return await services.adminApi.CopyBundleAsync(bundleId, await GetActorContext());
    }

    [HttpPost("asset/backport-from-roblox"), AdminPermission(Access.CreateAssetCopiedFromRoblox)]
    public async Task<AdminAssetIdResponse> BackportAssetFromRoblox([Required, FromBody] CopyAssetRequest request)
    {
        return await services.adminApi.BackportAssetFromRobloxAsync(request, await GetActorContext());
    }

    [HttpPost("asset/bulk-backport-from-roblox"), AdminPermission(Access.CreateAssetCopiedFromRoblox)]
    public async Task<BulkCopyAssetResponse> BackportAssetsFromRoblox([Required, FromBody] BulkCopyAssetRequest request)
    {
        return await services.adminApi.BackportAssetsFromRobloxAsync(request, await GetActorContext());
    }

    [HttpPost("asset/copy-from-roblox"), AdminPermission(Access.CreateAssetCopiedFromRoblox)]
    public async Task<AdminAssetIdResponse> CopyAssetFromRoblox([Required, FromBody] CopyAssetRequest request)
    {
        return await services.adminApi.CopyAssetFromRobloxAsync(request, await GetActorContext());
    }

    [HttpPost("asset/bulk-copy-from-roblox"), AdminPermission(Access.CreateAssetCopiedFromRoblox)]
    public async Task<BulkCopyAssetResponse> CopyAssetsFromRoblox([Required, FromBody] BulkCopyAssetRequest request)
    {
        return await services.adminApi.CopyAssetsFromRobloxAsync(request, await GetActorContext());
    }

    [HttpGet("ugc-requests/pending"), AdminPermission(Access.PendingUgcItems)]
    public async Task<IEnumerable<PendingUgcRequestEntry>> GetPendingUgcRequests()
    {
        return await services.adminApi.GetPendingUgcRequestsAsync();
    }

    [HttpPost("ugc-request/moderate"), AdminPermission(Access.PendingUgcItems)]
    public async Task<AdminSuccessResponse> ModerateUgcRequest([Required, FromBody] ModerateUgcRequestBody request)
    {
        return await services.adminApi.ModerateUgcRequestAsync(request, await GetActorContext());
    }

    [HttpPost("asset/create"), AdminPermission(Access.CreateAsset)]
    public async Task<CreateResponse> CreateAsset([Required, FromForm] CreateAssetRequest request)
    {
        return await services.adminApi.CreateAssetAsync(request);
    }

    [HttpPost("asset/create/clothing"), AdminPermission(Access.CreateClothingAsset)]
    public async Task<CreateResponse> CreateClothingAsset([Required, FromForm] CreateClothingRequest request)
    {
        return await services.adminApi.CreateClothingAssetAsync(request);
    }

    [HttpPost("asset/create/from-roblox"), AdminPermission(Access.MigrateAssetFromRoblox)]
    public async Task<MigrateItemResponse> CopyAnyItemFromRoblox([Required, FromBody] MigrateItemAlternateRequest request)
    {
        return await services.adminApi.MigrateAnyItemFromRobloxAsync(request, await GetActorContext());
    }

    [HttpGet("group-verify"), AdminPermission(Access.LockAndUnlockGroup)]
    public async Task<AdminMessageResponse> GroupVerify(long groupId, bool verify)
    {
        return await services.adminApi.GroupVerifyAsync(groupId, verify, await GetActorContext());
    }   

    [HttpGet("create-promocode"), AdminPermission(Access.GiveUserItem)]
    public async Task<AdminMessageResponse> CreatePromocode(string promocode, int? robux, long? assetId)
    {
        return await services.adminApi.CreatePromocodeAsync(promocode, robux, assetId, await GetActorContext());
    }

    [HttpGet("delete-promocode"), AdminPermission(Access.GiveUserItem)]
    public async Task<AdminMessageResponse> DeletePromocode(string promocode)
    {
        return await services.adminApi.DeletePromocodeAsync(promocode, await GetActorContext());
    }

    [HttpPost("create-game"), AdminPermission(Access.CreateGameForUser)]
    public async Task<AdminCreateGameResponse> CreateGame([Required, FromBody] UserIdRequest request)
    {
        return await services.adminApi.CreateGameAsync(request);
    }

    [HttpPost("asset/version/create"), AdminPermission(Access.CreateAssetVersion)]
    public async Task<AssetVersionWithIdEntry> CreateAssetVersion([Required, FromForm] CreateAssetVersionRequest request)
    {
        return await services.adminApi.CreateAssetVersionAsync(request, await GetActorContext());
    }

    [HttpPost("infrastructure/request-update"), AdminPermission(Access.RequestWebsiteUpdate)]
    public AdminMessageResponse RequestUpdate()
    {
        throw new Roblox.Services.Exceptions.RobloxException(500, 0, "Feature has been removed");
    }

    [HttpGet("feature-flags/all"), AdminPermission(Access.ManageFeatureFlags)]
    public IReadOnlyDictionary<FeatureFlag, bool> GetAllFlags()
    {
        return services.adminApi.GetAllFlags();
    }

    [HttpPost("feature-flags/enable"), AdminPermission(Access.ManageFeatureFlags)]
    public async Task EnableFlag(string featureFlag)
    {
        await services.adminApi.EnableFlagAsync(featureFlag);
    }

    [HttpPost("feature-flags/disable"), AdminPermission(Access.ManageFeatureFlags)]
    public async Task DisableFlag(string featureFlag)
    {
        await services.adminApi.DisableFlagAsync(featureFlag);
    }

    [HttpGet("players/in-game"), AdminPermission(Access.GetUsersInGame)]
    public async Task<IReadOnlyCollection<AdminDataRow>> GetInGamePlayers()
    {
        return await services.adminApi.GetInGamePlayersAsync();
    }

    [HttpGet("players/online-count"), AdminPermission(Access.GetUsersOnline)]
    public async Task<AdminTotalResponse> GetOnlinePlayersCount()
    {
        return await services.adminApi.GetOnlinePlayersCountAsync();
    }



    [HttpGet("users/{userId:long}/transactions"), AdminPermission(Access.GetUserTransactions)]
    public async Task<IEnumerable<TransactionEntryDb>> GetUserTransactions(long userId, PurchaseType type, int offset, int limit)
    {
        return await services.adminApi.GetUserTransactionsAsync(userId, type, offset, limit);
    }

    [HttpGet("users/{userId:long}/all-transactions"), AdminPermission(Access.GetUserTransactions)]
    public async Task<IEnumerable<TransactionEntryDb>> GetAllUserTransactions(long userId, int offset, int limit)
    {
        return await services.adminApi.GetAllUserTransactionsAsync(userId, offset, limit);
    }

    [HttpGet("users/{userId:long}/trades"), AdminPermission(Access.GetUserTransactions)]
    public async Task<IReadOnlyCollection<AdminTradeHistoryResponse>> GetUserTrades(long userId, TradeType type, int offset, int limit)
    {
        return await services.adminApi.GetUserTradesAsync(userId, type, offset, limit);
    }

    [HttpPost("trades/{tradeId:long}/rollback"), AdminPermission(Access.RollbackTrade)]
    public async Task RollbackTrade(long tradeId)
    {
        await services.adminApi.RollbackTradeAsync(tradeId, await GetActorContext());
    }

    [HttpPost("users/{userId:long}/reset-description"), AdminPermission(Access.ResetDescription)]
    public async Task ResetDescription(long userId)
    {
        await services.adminApi.ResetDescriptionAsync(userId);
    }

    [HttpPost("users/{userId:long}/reset-username"), AdminPermission(Access.ResetUsername)]
    public async Task ResetUsername(long userId)
    {
        await services.adminApi.ResetUsernameAsync(userId, await GetActorContext(), _staffAuthorization.IsOwner);
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


    [HttpGet("applications/update-lock"), AdminPermission(Access.ManageApplications)]
    public async Task UpdateLocks(string ids)
    {
        await services.adminApi.UpdateLocksAsync(ids, await GetActorContext());
    }

    [HttpGet("applications/list"), AdminPermission(Access.ManageApplications)]
    public async Task<IEnumerable<UserApplicationEntry>> GetApplications(UserApplicationStatus? status, int offset, SortOrder sortOrder, string? searchQuery = null, ApplicationSearchColumn? searchColumn = null)
    {
        return await services.adminApi.GetApplicationsAsync(status, offset, sortOrder, searchQuery, searchColumn, await GetActorContext());
    }

    [HttpGet("applications/details"), AdminPermission(Access.ManageApplications)]
    public async Task<UserApplicationEntry> GetApplicationById(string id)
    {
        return await services.adminApi.GetApplicationByIdAsync(id);
    }

    [HttpGet("applications/pending-num")]
    [SkipAdminTwoFactor]
    [AdminPermission(Access.ManageApplications)]
    public async Task<AdminCountResponse> GetNumPendingApplications()
    {
        return await services.adminApi.GetNumPendingApplicationsAsync();
    }

    [HttpPost("applications/{applicationId}/approve"), AdminPermission(Access.ManageApplications)]
    public async Task<AdminApplicationApproveResponse> ApproveApplication(string applicationId)
    {
        return await services.adminApi.ApproveApplicationAsync(applicationId, await GetActorContext());
    }

    [HttpPost("applications/{applicationId}/decline"), AdminPermission(Access.ManageApplications)]
    public async Task DeclineApplication(string applicationId, string reason)
    {
        await services.adminApi.DeclineApplicationAsync(applicationId, reason, await GetActorContext());
    }

    [HttpPost("applications/{applicationId}/decline-silent"), AdminPermission(Access.ManageApplications)]
    public async Task DeclineApplicationSilently(string applicationId)
    {
        await services.adminApi.DeclineApplicationSilentlyAsync(applicationId, await GetActorContext());
    }

    [HttpPost("applications/{applicationId}/clear"), AdminPermission(Access.ClearApplications)]
    public async Task ClearApplication(string applicationId)
    {
        await services.adminApi.ClearApplicationAsync(applicationId);
    }

    [HttpGet("invites/{userId:long}"), AdminPermission(Access.ManageInvites)]
    public async Task<IEnumerable<UserInviteEntry>> GetInvitesByUser(long userId)
    {
        return await services.adminApi.GetInvitesByUserAsync(userId);
    }

    [HttpGet("text-moderation/get-latest"), AdminPermission(Access.GetAllAssetComments)]
    public async Task<AdminLatestTextModerationIdsResponse> GetLatestIdsForTextMod()
    {
        return await services.adminApi.GetLatestIdsForTextModAsync();
    }

    [HttpGet("assets/comments"), AdminPermission(Access.GetAllAssetComments)]
    public async Task<IEnumerable<StaffAssetCommentEntry>> GetAllAssetComments(int limit, int offset, string? sortOrder = "asc", long? exclusiveStartId = 0)
    {
        return await services.adminApi.GetAllAssetCommentsAsync(limit, offset, sortOrder, exclusiveStartId);
    }

    [HttpGet("groups/wall"), AdminPermission(Access.GetGroupWall)]
    public async Task<IEnumerable<StaffWallEntry>> GetAllWallPosts(int limit, int offset, string? sortOrder = "asc", long? exclusiveStartId = null)
    {
        return await services.adminApi.GetAllWallPostsAsync(limit, offset, sortOrder, exclusiveStartId);
    }

    [HttpPost("groups/wall/remove"), AdminPermission(Access.DeleteGroupWallPost)]
    public async Task RemoveWallPost(long id)
    {
        await services.adminApi.RemoveWallPostAsync(id);
    }

    [HttpGet("groups/status"), AdminPermission(Access.GetGroupStatus)]
    public async Task<IEnumerable<GroupWallPostStaff>> GetGroupStatuses(int offset, int limit, string? sortOrder = "asc", long? exclusiveStartId = null)
    {
        return await services.adminApi.GetGroupStatusesAsync(offset, limit, sortOrder, exclusiveStartId);
    }

    [HttpPost("groups/status/delete"), AdminPermission(Access.DeleteGroupStatus)]
    public async Task DeleteGroupStatus(long id)
    {
        await services.adminApi.DeleteGroupStatusAsync(id);
    }

    [HttpGet("users/status"), AdminPermission(Access.GetAllUserStatuses)]
    public async Task<IEnumerable<StaffUserStatusEntry>> GetAllUserStatuses(int offset, int limit, string? sortOrder = "asc", long? exclusiveStartId = null)
    {
        return await services.adminApi.GetAllUserStatusesAsync(offset, limit, sortOrder, exclusiveStartId);
    }

    [HttpGet("groups/list"), AdminPermission(Access.GetGroupManageInfo)]
    public async Task<IReadOnlyCollection<AdminDataRow>> GetGroupList(int offset, int limit, string sortColumn, string sortOrder)
    {
        return await services.adminApi.GetGroupListAsync(offset, limit, sortColumn, sortOrder);
    }

    [HttpGet("groups/get-by-name"), AdminPermission(Access.GetGroupManageInfo)]
    public async Task<AdminGroupModerationInfoResponse> GetGroupByName(string name)
    {
        return await services.adminApi.GetGroupByNameAsync(name);
    }

    [HttpGet("groups/audit-log"), AdminPermission(Access.GetGroupManageInfo)]
    public async Task<IReadOnlyCollection<AdminDataRow>> GetEntireAuditLog(long groupId)
    {
        return await services.adminApi.GetEntireAuditLogAsync(groupId);
    }

    [HttpPost("groups/toggle-lock-status"), AdminPermission(Access.LockAndUnlockGroup)]
    public async Task ToggleGroupLockStatus(long groupId, bool locked)
    {
        await services.adminApi.ToggleGroupLockStatusAsync(groupId, locked);
    }

    [HttpPost("groups/reset"), AdminPermission(Access.ResetGroup)]
    public async Task ResetGroup(long groupId)
    {
        await services.adminApi.ResetGroupAsync(groupId);
    }

    [HttpGet("games/play-history"), AdminPermission(Access.GetUsersInGame)]
    public async Task<IReadOnlyCollection<AdminDataRow>> GetPlayHistory(int limit, int offset)
    {
        return await services.adminApi.GetPlayHistoryAsync(limit, offset);
    }

    [HttpPost("text-moderation/request-payment"), AdminPermission(Access.GetAllAssetComments)]
    public async Task<AdminRobuxAmountResponse> RequestPayment()
    {
        return await services.adminApi.RequestPaymentAsync(await GetActorContext());
    }

    [HttpGet("chat-messages/{reportId}"), AdminPermission(Access.ManageReports)]
    public async Task<IActionResult> GetChatMessages(string reportId)
    {
        var response = await services.adminApi.GetChatMessagesAsync(reportId);
        return Content(response.content, response.contentType, Encoding.UTF8);
    }

    [HttpGet("reports/pending-count"), AdminPermission(Access.ManageReports)]
    [SkipAdminTwoFactor]
    public async Task<AdminCountResponse> GetPendingReports()
    {
        return await services.adminApi.GetPendingReportsAsync();
    }

    [HttpGet("reports/list"), AdminPermission(Access.ManageReports)]
    public async Task<IEnumerable<AbuseReportEntry>> GetReports(AbuseReportStatus status)
    {
        return await services.adminApi.GetReportsAsync(status);
    }

    [HttpPost("reports/{id}/accept"), AdminPermission(Access.ManageReports)]
    public async Task AcceptReport(string id)
    {
        await services.adminApi.AcceptReportAsync(id, await GetActorContext());
    }

    [HttpPost("reports/{id}/decline"), AdminPermission(Access.ManageReports)]
    public async Task DeclineReport(string id)
    {
        await services.adminApi.DeclineReportAsync(id, await GetActorContext());
    }

    [HttpPost("reports/{id}/invalid"), AdminPermission(Access.ManageReports)]
    public async Task DeclineReportInvalid(string id)
    {
        await services.adminApi.DeclineReportInvalidAsync(id, await GetActorContext());
    }

    [HttpGet("assets/{assetId}/owners"), AdminPermission(Access.GetAllAssetOwners)]
    public async Task<IEnumerable<CollectibleUserAssetEntry>> GetLiterallyAllOwnersKindaUnsafe(long assetId)
    {
        return await services.adminApi.GetAllOwnersAsync(assetId);
    }

    [HttpGet("moderation/get-by-thumbnail"), AdminPermission(Access.GetDetailsFromThumbnail)]
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
    
    [HttpGet("performance/totals/assets"), AdminPermission(Access.GetStaffPerformance)]
    public async Task<long> GetPerfTotalsAsset(long userId)
    {
        return await services.adminApi.GetPerfTotalsAssetAsync(userId);
    }
    
    [HttpGet("performance/totals/audios"), AdminPermission(Access.GetStaffPerformance)]
    public async Task<long> GetPerfTotalsAudios(long userId)
    {
        return await services.adminApi.GetPerfTotalsAudiosAsync(userId);
    }
    
    [HttpGet("performance/totals/signups"), AdminPermission(Access.GetStaffPerformance)]
    public async Task<long> GetPerfTotalsApplications(long userId)
    {
        return await services.adminApi.GetPerfTotalsApplicationsAsync(userId);
    }
    
    [HttpGet("performance/totals/reports"), AdminPermission(Access.GetStaffPerformance)]
    public async Task<long> GetPerfTotalsReports(long userId)
    {
        return await services.adminApi.GetPerfTotalsReportsAsync(userId);
    }
    
    [HttpGet("performance/totals/players-moderated"), AdminPermission(Access.GetStaffPerformance)]
    public async Task<long> GetPerfTotalsPlayersModerated(long userId)
    {
        return await services.adminApi.GetPerfTotalsPlayersModeratedAsync(userId);
    }
    
    [HttpGet("performance/permissions-gave"), AdminPermission(Access.GetStaffPerformance)]
    public async Task<AdminDateResponse> GetPerfPermDate(long userId)
    {
        return await services.adminApi.GetPerfPermDateAsync(userId);
    }
}
