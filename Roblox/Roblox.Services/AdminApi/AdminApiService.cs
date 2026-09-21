using System.Collections.Concurrent;
using Dapper;
using Roblox.Cache;
using Roblox.Dto;
using Roblox.Dto.AbuseReport;
using Roblox.Dto.Admin;
using Roblox.Dto.Assets;
using Roblox.Dto.Avatar;
using Roblox.Dto.Economy;
using Roblox.Dto.Groups;
using Roblox.Dto.Staff;
using Roblox.Dto.Users;
using Roblox.Exceptions;
using Roblox.Libraries;
using Roblox.Libraries.DiscordApi;
using Roblox.Libraries.RobloxApi;
using Roblox.Logging;
using Roblox.Models.AbuseReport;
using Roblox.Models.Assets;
using Roblox.Models.Avatar;
using Roblox.Models.Db;
using Roblox.Models.Economy;
using Roblox.Models.Staff;
using Roblox.Models.Trades;
using Roblox.Models.Users;
using Roblox.Services.App.FeatureFlags;
using Roblox.Services.Assets;
using Roblox.Services.Exceptions;
using System.Diagnostics;
using System.Net.NetworkInformation;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using AssetMultiGetEntry = Roblox.Dto.Assets.MultiGetEntry;
using Type = Roblox.Models.Assets.Type;

namespace Roblox.Services.AdminApi;

public class AdminApiService : ServiceBase
{
    private static readonly long startTime = DateTimeOffset.Now.ToUnixTimeSeconds();
    private const int MaxBulkRobloxAssetCopyCount = 50;
    private const int DefaultBulkRobloxAssetCopyPriceRobux = 30;
    private const int AdminTwoFactorVerifyRequestsPerMinute = 10;
    private const int AdminTwoFactorInvalidAttemptsPerWindow = 5;
    private static readonly TimeSpan AdminTwoFactorInvalidAttemptWindow = TimeSpan.FromMinutes(10);
    private static readonly TimeSpan AdminTwoFactorCompromiseAlertWindow = TimeSpan.FromHours(1);
    private static readonly Regex matchAssetThumbRegex = new("\\/images\\/thumbnails\\/([a-zA-Z0-9]+)", RegexOptions.Compiled);
    private static readonly Regex matchUserThumbRegex = new("(\\/images\\/thumbnails\\/[a-zA-Z0-9\\.\\\\_]+)", RegexOptions.Compiled);
    private static readonly Regex matchGroupIconRegex = new("\\/images\\/thumbnails\\/([a-zA-Z0-9\\.]+)", RegexOptions.Compiled);
    private static readonly Regex migrateItemAssetIdUrlRegex = new("\\?id=([0-9]+)", RegexOptions.Compiled);
    private static readonly List<string> whitelistedUserSorts = new()
    {
        "user_economy.balance_robux",
        "user_economy.balance_tickets",
        "user.id",
        "user.online_at",
    };
    private static readonly List<string> allowedGroupSortColumns = new()
    {
        "id",
    };

    private AbuseReportService? _abuseReport;
    private AccountInformationService? _accountInformation;
    private AssetsService? _assets;
    private AvatarService? _avatar;
    private CooldownService? _cooldown;
    private EconomyService? _economy;
    private ForumsService? _forums;
    private GamesService? _games;
    private GameServerService? _gameServer;
    private GroupsService? _groups;
    private PrivateMessagesService? _privateMessages;
    private PromocodesService? _promocodes;
    private RobloxApi? _robloxApi;
    private TradesService? _trades;
    private UsersService? _users;
    private DiscordBotApi? _discordBotApi;
    private MachineBanService? _machineBan;
    private PermanentAccountTerminationService? _permanentTermination;

    private AbuseReportService abuseReport => _abuseReport ??= ServiceProvider.GetOrCreate<AbuseReportService>(this);
    private AccountInformationService accountInformation => _accountInformation ??= ServiceProvider.GetOrCreate<AccountInformationService>(this);
    private AssetsService assets => _assets ??= ServiceProvider.GetOrCreate<AssetsService>(this);
    private AvatarService avatar => _avatar ??= ServiceProvider.GetOrCreate<AvatarService>(this);
    private CooldownService cooldown => _cooldown ??= ServiceProvider.GetOrCreate<CooldownService>(this);
    private EconomyService economy => _economy ??= ServiceProvider.GetOrCreate<EconomyService>(this);
    private ForumsService forums => _forums ??= ServiceProvider.GetOrCreate<ForumsService>(this);
    private GamesService games => _games ??= ServiceProvider.GetOrCreate<GamesService>(this);
    private GameServerService gameServer => _gameServer ??= ServiceProvider.GetOrCreate<GameServerService>(this);
    private GroupsService groups => _groups ??= ServiceProvider.GetOrCreate<GroupsService>(this);
    private PrivateMessagesService privateMessages => _privateMessages ??= ServiceProvider.GetOrCreate<PrivateMessagesService>(this);
    private PromocodesService promocodes => _promocodes ??= ServiceProvider.GetOrCreate<PromocodesService>(this);
    private RobloxApi robloxApi => _robloxApi ??= new RobloxApi();
    private TradesService trades => _trades ??= ServiceProvider.GetOrCreate<TradesService>(this);
    private UsersService users => _users ??= ServiceProvider.GetOrCreate<UsersService>(this);
    private DiscordBotApi discordBotApi => _discordBotApi ??= new DiscordBotApi(Roblox.Configuration.DiscordBotToken);
    private MachineBanService machineBan => _machineBan ??= ServiceProvider.GetOrCreate<MachineBanService>(this);
    private PermanentAccountTerminationService permanentTermination =>
        _permanentTermination ??= ServiceProvider.GetOrCreate<PermanentAccountTerminationService>(this);

    private static AdminDataRow ToAdminRow(object row)
    {
        var result = new AdminDataRow();
        if (row is IDictionary<string, object?> nullableDictionary)
        {
            foreach (var (key, value) in nullableDictionary)
                result[key] = value;
            return result;
        }

        if (row is IDictionary<string, object> dictionary)
        {
            foreach (var (key, value) in dictionary)
                result[key] = value;
            return result;
        }

        foreach (var property in row.GetType().GetProperties())
            result[property.Name] = property.GetValue(row);

        return result;
    }

    private static IReadOnlyCollection<AdminDataRow> ToAdminRows(IEnumerable<object> rows)
    {
        return rows.Select(ToAdminRow).ToArray();
    }

    private static void ConvertEnumField<TEnum>(AdminDataRow row, string key) where TEnum : struct, Enum
    {
        if (!row.TryGetValue(key, out var value) || value == null)
            return;

        row[key] = value is TEnum enumValue
            ? enumValue.ToString()
            : Enum.ToObject(typeof(TEnum), value).ToString();
    }

    private static bool HasPermission(AdminActorContext actor, Access permission)
    {
        return actor.isOwner || actor.permissions.Contains(permission);
    }

    private async Task<bool> IsStaffAsync(long userId, Func<long, bool> isOwnerUserId)
    {
        return isOwnerUserId(userId) || (await users.GetStaffPermissions(userId)).Any();
    }

    private static void RequireOwner(AdminActorContext actor, string message)
    {
        if (!actor.isOwner)
            throw new StaffException(message);
    }

    public string GetImageUrl(string fileName, bool isThumbnails = true)
    {
        if (fileName.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
            fileName.StartsWith("https://", StringComparison.OrdinalIgnoreCase) ||
            fileName.StartsWith("/img/", StringComparison.OrdinalIgnoreCase))
        {
            return fileName;
        }

        var baseUrl = string.IsNullOrWhiteSpace(Roblox.Configuration.CdnBaseUrl)
            ? "https://cdn.averia.lol/"
            : Roblox.Configuration.CdnBaseUrl;

        baseUrl = baseUrl.TrimEnd('/') + "/";

        if (fileName.StartsWith('/'))
        {
            fileName = fileName[1..];
        }

        if (fileName.StartsWith("images/", StringComparison.OrdinalIgnoreCase))
        {
            return baseUrl + fileName;
        }

        if (fileName.StartsWith("groups/", StringComparison.OrdinalIgnoreCase))
        {
            return baseUrl + "images/" + EnsurePngExtension(fileName);
        }

        if (fileName.StartsWith("thumbnails/", StringComparison.OrdinalIgnoreCase))
        {
            return baseUrl + "images/" + EnsurePngExtension(fileName);
        }

        var prefix = isThumbnails ? "images/thumbnails/" : "images/groups/";
        return baseUrl + prefix + EnsurePngExtension(fileName);
    }

    private static string EnsurePngExtension(string fileName)
    {
        return fileName.EndsWith(".png", StringComparison.OrdinalIgnoreCase) ||
               fileName.EndsWith(".json", StringComparison.OrdinalIgnoreCase)
            ? fileName
            : fileName + ".png";
    }

    public async Task<AdminPermissionsResponse> GetPermissionsAsync(AdminActorContext actor)
    {
        var permissions = actor.isOwner
            ? Enum.GetValues<Access>()
            : (await users.GetStaffPermissions(actor.userId)).Select(c => c.permission);
        var isAdmin = actor.isOwner;
        var isMod = isAdmin;

        return new AdminPermissionsResponse
        {
            rank = new AdminRankResponse
            {
                name = actor.isOwner ? "Owner" : isAdmin ? "admin" : isMod ? "Mod" : null,
                details = new AdminRankDetailsResponse
                {
                    isAdmin = isAdmin,
                    isModerator = isMod,
                    isOwner = actor.isOwner,
                },
                permissions = permissions,
            },
        };
    }

    public async Task ValidateTwoFactorCodeAsync(long userId, string sessionId, string code)
    {
        if (!await cooldown.TryIncrementBucketCooldown(
                $"AdminTwoFactorVerifyAttemptV1:{userId}:{sessionId}",
                AdminTwoFactorVerifyRequestsPerMinute,
                TimeSpan.FromMinutes(1),
                true))
        {
            throw new TooManyRequestsException("Too many 2FA verification attempts. Try again in a minute.");
        }

        var totp = await users.GetTotp(userId);
        if (totp == null || string.IsNullOrWhiteSpace(code) || !users.VerifyTotp(totp.secret, code))
        {
            await RecordInvalidTwoFactorAttemptAsync(userId);
            throw new UnauthorizedException();
        }
    }

    private async Task RecordInvalidTwoFactorAttemptAsync(long userId)
    {
        var allowed = await cooldown.TryIncrementBucketCooldown(
            $"AdminTwoFactorInvalidAttemptV1:{userId}",
            AdminTwoFactorInvalidAttemptsPerWindow,
            AdminTwoFactorInvalidAttemptWindow,
            true);

        if (allowed)
            return;

        await SendPotentialStaffCompromiseAlertAsync(userId);
        throw new TooManyRequestsException("Too many invalid 2FA verification attempts. Try again later.");
    }

    private async Task SendPotentialStaffCompromiseAlertAsync(long userId)
    {
        if (!await cooldown.TryIncrementBucketCooldown(
                $"AdminTwoFactorCompromiseAlertV1:{userId}",
                1,
                AdminTwoFactorCompromiseAlertWindow))
        {
            return;
        }

        await discordBotApi.SendMessageInChannel(
            Roblox.Configuration.DiscordLogChannelId,
            $"<@1339179586407235680> :warning: STAFF {userId} POTENTIAL COMPROMISE!");
    }

    public Task<IEnumerable<UserId>> GetAllStaffAsync()
    {
        return users.GetAllStaff();
    }

    public IEnumerable<Access> GetAllPermissions()
    {
        return Enum.GetValues<Access>();
    }

    public Task<IEnumerable<StaffUserPermissionEntry>> GetUserPermissionsAsync(long userId)
    {
        return users.GetStaffPermissions(userId);
    }

    public async Task SetUserPermissionsAsync(long userId, Access permission, AdminActorContext actor)
    {
        if (permission == Access.SetPermissions)
            throw new StaffException("InternalServerError");

        if (!actor.isOwner)
            throw new Exception("InternalServerError");

        if (permission == Access.All)
            throw new BadRequestException(0, "Invalid permission");

        await users.AddStaffPermission(userId, permission);
    }

    public Task DeletePermissionAsync(long userId, Access permission)
    {
        return users.RemoveStaffPermission(userId, permission);
    }

    public AdminStatsResponse GetStatus()
    {
        using var proc = Process.GetCurrentProcess();
        var gcInfo = GC.GetGCMemoryInfo();
        var allocatedMem = proc.WorkingSet64;
        var memoryInUse = gcInfo.HeapSizeBytes;
        return new AdminStatsResponse
        {
            memory = new AdminMemoryStatsResponse
            {
                allocated = (allocatedMem / 1024 / 1024) + " KB",
                used = (memoryInUse / 1024 / 1024) + " KB",
            },
            serverStartTime = startTime,
        };
    }

    public void CrashSite(AdminActorContext actor)
    {
        if (!actor.isOwner)
            throw new UnauthorizedException();

        Environment.Exit(0);
    }

    public async Task<AdminSystemMessageResponse> GetSystemMessageAsync()
    {
        var msg = await users.GetGlobalAlert();
        return new AdminSystemMessageResponse
        {
            LinkText = "",
            LinkUrl = msg?.url ?? "",
            Text = msg?.message ?? "",
            IsVisible = msg != null,
        };
    }

    public async Task SetAlertAsync(SetAlertRequest request, AdminActorContext actor)
    {
        if (request.text == "")
            request.text = null;
        if (request.text is { Length: > 255 })
            throw new StaffException("Text is over the limit of 255 characters");
        if (request.url is { Length: > 255 })
            throw new StaffException("URL is over 255 characters");
        if (string.IsNullOrWhiteSpace(request.url))
            request.url = null;

        Writer.Info(LogGroup.AbuseDetection, "User {0} is setting alert to '{1}'", actor.userId, request.text);
        await users.SetGlobalAlert(request.text, request.url);
        await db.ExecuteAsync("INSERT INTO moderation_set_alert (actor_id, alert, alert_url) VALUES (:user_id, :text, :url)", new
        {
            user_id = actor.userId,
            text = request.text,
            url = request.url,
        });
    }

    public async Task<UserId> CreateUserAsync(CreateUserRequest request)
    {
        if (request.username == null)
            throw new StaffException("Bad username");
        if (request.password == null)
            throw new StaffException("Bad password");

        int? userId = null;
        if (int.TryParse(request.userId, out var parsedUserId))
            userId = parsedUserId;

        return await users.CreateUser(request.username, request.password, Gender.Unknown, userId);
    }

    public async Task<AdminMessageResponse> ForceApplicationAsync(ForceApplicationReq request)
    {
        if (request.socialURL == null)
            throw new StaffException("Bad Social URL");

        var inviteId = users.GetUserInvite(request.userId);
        if (inviteId != null)
            await users.DeleteUserInvite(request.userId);

        var id = await users.CreateApplication(new CreateUserApplicationRequest
        {
            about = "Forced Application",
            socialPresence = request.socialURL,
            isVerified = true,
            verifiedUrl = request.socialURL,
            verificationPhrase = "Forced Application",
            verifiedId = "1",
        });

        var joinId = await users.ProcessApplication(id, 1, UserApplicationStatus.Approved);
        if (joinId == null)
            throw new StaffException("The join id is null");

        await users.SetApplicationUserIdByJoinId(joinId, request.userId);
        return new AdminMessageResponse { message = "Join application added to user" };
    }

    public async Task<IReadOnlyCollection<PendingGroupIconEntry>> GetPendingIconsAsync()
    {
        var result = (await db.QueryAsync<PendingGroupIconEntry>(
            "SELECT group_icon.group_id as group_id, group_icon.name, group_icon.user_id as user_id, u.username as creatorName FROM group_icon INNER JOIN \"user\" u ON u.id = group_icon.user_id WHERE is_approved = 0 ORDER BY group_id"))
            .ToList();

        foreach (var item in result)
        {
            item.name = GetImageUrl("/images/groups/" + item.name, false);
        }

        return result;
    }

    public async Task GiftUsersAsync(GiftUsersRequest request, long actorUserId, bool actorIsOwner)
    {
        if (!actorIsOwner)
            throw new StaffException("You are not allowed to do that");

        if (!await cooldown.TryIncrementBucketCooldown("GiftGlobalLimitV3", 100, TimeSpan.FromHours(12)))
            throw new StaffException("You hit the global rate limit");
        if (!await cooldown.TryIncrementBucketCooldown($"SameGiftV2:{request.giftId}", 1, TimeSpan.FromHours(12)))
            throw new StaffException("You already gifted the same item");

        var details = await assets.GetAssetCatalogInfo(request.assetId);
        if (details.itemRestrictions.Contains("LimitedUnique") || details.itemRestrictions.Contains("Limited"))
            throw new StaffException("This item is a limited");

        var giftOwners = await db.QueryAsync<CollectibleUserAssetEntry>(
            "SELECT id AS userAssetId, asset_id AS assetId, user_id AS userId, price, serial, created_at AS createdAt, updated_at AS updatedAt FROM user_asset WHERE asset_id = :giftId",
            new
            {
                giftId = request.giftId,
            });
        var assetInfo = await assets.GetAssetCatalogInfo(request.assetId);
        foreach (var owner in giftOwners)
        {
            long? serial = null;
            var userAssetId = await db.QuerySingleOrDefaultAsync<long>(
                "INSERT INTO user_asset (asset_id, user_id, serial) VALUES (:assetId, :userId, :serial) RETURNING id",
                new
                {
                    assetId = request.assetId,
                    userId = owner.userId,
                    serial,
                });

            await economy.InsertTransaction(new AssetPurchaseTransaction(owner.userId, assetInfo.creatorType,
                assetInfo.creatorTargetId, CurrencyType.Robux, assetInfo.price ?? 0, assetInfo.id, userAssetId));
            await economy.InsertTransaction(new AssetSaleTransaction(owner.userId, assetInfo.creatorType,
                assetInfo.creatorTargetId, CurrencyType.Robux, assetInfo.price ?? 0, assetInfo.id, userAssetId));

            await assets.IncrementSaleCount(request.assetId);
        }
    }

    public async Task<PendingAssetEntry> GetModerationDetailsAsync(long assetId, Func<long, bool> isOwnerUserId)
    {
        var item = await db.QuerySingleOrDefaultAsync<PendingAssetEntry>(
            "SELECT asset.id, asset.name, asset_thumbnail.content_url, asset.asset_type as assetType FROM asset LEFT JOIN asset_thumbnail ON asset_thumbnail.asset_id = asset.id WHERE asset.id = :id",
            new
            {
                id = assetId,
            });
        if (item == null)
            throw new StaffException("Asset ID is invalid");

        var assetInfo = await assets.GetAssetCatalogInfo(assetId);
        var latestVersion = await TryGetLatestAssetVersionAsync(assetId);
        var (creatorId, creatorName) = await ResolveCreatorAsync(assetInfo, latestVersion);
        item.creatorId = creatorId;
        item.creatorName = creatorName;

        if (item.content_url == null && AssetRenderQueue.IsRenderable(item.assetType))
        {
            await assets.RenderAssetsInBurstAsync(new[] { (item.id, item.assetType) });
            item.content_url = await db.QuerySingleOrDefaultAsync<string?>(
                "SELECT content_url FROM asset_thumbnail WHERE asset_id = :assetId ORDER BY id DESC LIMIT 1",
                new { assetId = item.id });
        }

        if (item.content_url != null)
        {
            item.content_url = GetImageUrl("/images/thumbnails/" + item.content_url + ".png");
        }

        return item;
    }

    public async Task<Stream> GetPendingAssetStreamAsync(long assetId, long actorUserId, bool actorIsOwner)
    {
        var assetInfo = await assets.GetAssetCatalogInfo(assetId);
        if (assetInfo.moderationStatus != ModerationStatus.AwaitingApproval && !actorIsOwner)
            throw new StaffException("Item is not pending: " + assetInfo.moderationStatus);
        if (assetInfo.assetType != Type.Audio && assetInfo.assetType != Type.Video && assetInfo.assetType != Type.Model)
            throw new StaffException("Only videos/audios are allowed");

        var version = await assets.GetLatestAssetVersion(assetId);
        if (version.contentUrl == null)
            throw new StaffException("Unsupported action");

        return await assets.GetAssetContent(version.contentUrl);
    }

    public async Task<IReadOnlyCollection<PendingAssetEntry>> GetPendingAssetsAsync(long actorUserId, bool actorIsOwner, Func<long, bool> isOwnerUserId)
    {
        var offset = 0;
        var result = new List<PendingAssetEntry>();

        while (result.Count < 10)
        {
            var query = new SqlBuilder();
            var template = query.AddTemplate(
                "SELECT asset.id, asset.name, asset_thumbnail.content_url, asset.asset_type as assetType FROM asset LEFT JOIN asset_thumbnail ON asset_thumbnail.asset_id = asset.id /**where**/ ORDER BY asset.id LIMIT 10 OFFSET :offset");
            query.OrWhereMulti("(asset.moderation_status = :status AND asset.asset_type = $1)", new[]
            {
                Type.Image,
                Type.Decal,
                Type.Audio,
                Type.Face,
                Type.Mesh,
                Type.Lua,
                Type.Model,
                Type.Package,
                Type.Place,
                Type.Plugin,
                Type.Mesh,
                Type.SolidModel,
                Type.Video,
                Type.GamePass,
                Type.Badge,
            });
            query.AddParameters(new
            {
                status = ModerationStatus.AwaitingApproval,
                offset,
            });

            var firstPass = (await db.QueryAsync<PendingAssetEntry>(template.RawSql, template.Parameters)).ToList();
            if (firstPass.Count == 0)
                break;

            offset += firstPass.Count;
            foreach (var item in firstPass)
            {
                var details = await assets.GetAssetCatalogInfo(item.id);
                var latest = await TryGetLatestAssetVersionAsync(item.id);
                var (creatorId, creatorName) = await ResolveCreatorAsync(details, latest);
                item.creatorId = creatorId;
                if (item.creatorId == actorUserId && !actorIsOwner)
                    continue;

                item.creatorName = creatorName;
                result.Add(item);
            }
        }

        var missingThumbnails = result
            .Where(item => item.content_url == null && AssetRenderQueue.IsRenderable(item.assetType))
            .ToArray();
        if (missingThumbnails.Length != 0)
        {
            try
            {
                await assets.RenderAssetsInBurstAsync(
                    missingThumbnails.Select(item => (item.id, item.assetType)));
            }
            catch (Exception e)
            {
                Writer.Info(LogGroup.AdminApi,
                    "One or more moderation thumbnail burst renders failed: {0}\n{1}", e.Message, e.StackTrace);
            }

            var renderedThumbnails = (await db.QueryAsync<PendingAssetThumbnailRow>(
                    @"SELECT asset_id AS ""assetId"", content_url AS ""contentUrl""
                      FROM asset_thumbnail
                      WHERE asset_id = ANY(:assetIds)",
                    new { assetIds = missingThumbnails.Select(item => item.id).ToArray() }))
                .Where(item => item.contentUrl != null)
                .GroupBy(item => item.assetId)
                .ToDictionary(group => group.Key, group => group.Last().contentUrl!);

            foreach (var item in missingThumbnails)
            {
                if (renderedThumbnails.TryGetValue(item.id, out var contentUrl))
                    item.content_url = contentUrl;
            }
        }

        foreach (var item in result.Where(item => item.content_url != null))
            item.content_url = GetImageUrl("/images/thumbnails/" + item.content_url + ".png");

        return result;
    }

    private sealed class PendingAssetThumbnailRow
    {
        public long assetId { get; set; }
        public string? contentUrl { get; set; }
    }

    public async Task ModerateAssetAsync(ModerateAssetRequest request, long actorUserId, bool actorIsOwner, Func<long, bool> isOwnerUserId)
    {
        var details = await db.QuerySingleOrDefaultAsync<AssetModerationStatus>(
            "SELECT moderation_status as moderationStatus, roblox_asset_id as robloxAssetId FROM asset WHERE asset.id = :id",
            new
            {
                id = request.assetId,
            });
        if (details == null)
            throw new StaffException("Asset ID is invalid");

        if (!await cooldown.TryIncrementBucketCooldown($"ModerateApprovedItem_Hour:{actorUserId}:{request.assetId}",
                3, TimeSpan.FromHours(1)))
        {
            await discordBotApi.SendMessageInChannel(Roblox.Configuration.DiscordLogChannelId,
                $"## POSSIBLE ABUSE\nStaff member {actorUserId} has accepted Asset Id {request.assetId} more than three times in one hour.");
            throw new StaffException("Moderation of same asset rate limit exceeded, please wait and try again later.");
        }

        var currentStatus = details.moderationStatus;
        if (currentStatus == ModerationStatus.ReviewApproved && !request.isApproved && !actorIsOwner)
        {
            if (!await cooldown.TryIncrementBucketCooldown($"ModerateApprovedItem_Hour:{actorUserId}", 250, TimeSpan.FromHours(1)))
                throw new StaffException("Moderation of already approved item rate limit exceeded (hour). Contact an administrator.");
            if (!await cooldown.TryIncrementBucketCooldown($"ModerateApprovedItem_Day:{actorUserId}", 500, TimeSpan.FromDays(1)))
                throw new StaffException("Moderation of already approved item rate limit exceeded (day). Contact an administrator.");
            if (!await cooldown.TryIncrementBucketCooldown("ModerateApprovedItem_Day_Global", 5000, TimeSpan.FromDays(1)))
                throw new StaffException("Moderation of already approved item rate limit exceeded (day). Contact an administrator.");
        }

        var latest = await assets.GetLatestAssetVersion(request.assetId);
        var assetInfo = await assets.GetAssetCatalogInfo(request.assetId);

        if (!request.isApproved)
        {
            var isOwnerCreatedAsset = assetInfo.creatorType == CreatorType.User && isOwnerUserId(assetInfo.creatorTargetId);
            if (isOwnerCreatedAsset && !actorIsOwner)
                throw new StaffException("You do not have permission to delete items created by an owner");
        }

        if (latest.creatorId == actorUserId && !actorIsOwner)
            throw new StaffException("You cannot moderate your own assets");

        if (assetInfo.assetType == Type.Audio && details.canEarnRobuxFromApproval)
        {
            await AwardCommissionForModerationAsync(actorUserId);
        }

        var newStatus = request.isApproved ? ModerationStatus.ReviewApproved : ModerationStatus.Declined;
        await db.ExecuteAsync("UPDATE asset SET moderation_status = :status, is_18_plus = :is_18_plus WHERE id = :id",
            new
            {
                is_18_plus = request.is18Plus,
                status = newStatus,
                id = request.assetId,
            });

        if (newStatus == ModerationStatus.ReviewApproved)
        {
            await db.ExecuteAsync("UPDATE asset_media SET is_approved = TRUE WHERE media_asset_id = :id",
                new
                {
                    id = request.assetId,
                });
        }

        if (assetInfo.assetType == Type.Place && newStatus != ModerationStatus.ReviewApproved)
        {
            var gameServers = await gameServer.GetGameServersForPlace(assetInfo.id);
            foreach (var server in gameServers)
            {
                await gameServer.ShutDownServerAsync(server.id);
            }
        }

        await assets.InsertAssetModerationLog(request.assetId, actorUserId, newStatus);
        var children = (await db.QueryAsync<AssetIdRow>(
            "SELECT DISTINCT asset_id as assetId FROM asset_version WHERE content_id = :id",
            new
            {
                id = request.assetId,
            })).ToArray();
        foreach (var item in children)
        {
            await db.ExecuteAsync("UPDATE asset SET moderation_status = :status, is_18_plus = :is_18_plus WHERE id = :id",
                new
                {
                    is_18_plus = request.is18Plus,
                    status = newStatus,
                    id = item.assetId,
                });
            await assets.InsertAssetModerationLog(item.assetId, actorUserId, newStatus);
        }

        if (details.robloxAssetId != null && details.robloxAssetId != 0)
        {
            var duplicates = await db.QueryAsync<AssetIdRow>(
                "SELECT id as assetId FROM asset WHERE roblox_asset_id = :id",
                new
                {
                    id = details.robloxAssetId.Value,
                });
            foreach (var duplicate in duplicates)
            {
                await db.ExecuteAsync("UPDATE asset SET moderation_status = :status, is_18_plus = :is_18_plus WHERE id = :id",
                    new
                    {
                        is_18_plus = request.is18Plus,
                        status = newStatus,
                        id = duplicate.assetId,
                    });
                await assets.InsertAssetModerationLog(duplicate.assetId, actorUserId, newStatus);
            }
        }
    }

    public async Task ModerateAndDeleteItemAsync(ModerateAssetRequest request, long actorUserId, bool actorIsOwner, Func<long, bool> isOwnerUserId)
    {
        if (!actorIsOwner)
        {
            if (!await cooldown.TryIncrementBucketCooldown("DeleteAssetV1_Hour", 250, TimeSpan.FromHours(1)))
                throw new StaffException("Asset deletion rate limit exceeded (hour). Contact an administrator.");
            if (!await cooldown.TryIncrementBucketCooldown("DeleteAssetV1_Day", 500, TimeSpan.FromDays(1)))
                throw new StaffException("Asset deletion rate limit exceeded (day). Contact an administrator.");
            if (!await cooldown.TryIncrementBucketCooldown("DeleteAssetV1_Global", 5000, TimeSpan.FromDays(1)))
                throw new StaffException("Asset deletion rate limit exceeded (global). Contact an administrator.");
        }

        await ModerateAssetAsync(request, actorUserId, actorIsOwner, isOwnerUserId);
        if (!request.isApproved)
        {
            await assets.DeleteAsset(request.assetId);
        }
    }

    public async Task<IReadOnlyCollection<PendingAssetIconEntry>> GetPendingAssetIconsAsync()
    {
        var firstPass = (await db.QueryAsync<PendingAssetIconEntry>(
            "SELECT asset_icon.id, asset.name, asset_icon.content_url, asset_icon.asset_id as asset_id FROM asset_icon INNER JOIN asset ON asset.id = asset_icon.asset_id WHERE asset_icon.moderation_status = :status ORDER BY asset.id LIMIT 10",
            new
            {
                status = ModerationStatus.AwaitingApproval,
            })).ToList();
        if (firstPass.Count == 0)
            return firstPass;

        foreach (var item in firstPass)
        {
            try
            {
                var latest = await assets.GetLatestAssetVersion(item.asset_id);
                item.creatorId = latest.creatorId;
                item.creatorName = (await users.GetUserById(latest.creatorId)).username;
            }
            catch (Exception)
            {
                item.creatorId = 1;
                item.creatorName = "ROBLOX";
            }

            if (item.content_url != null)
            {
                item.content_url = GetImageUrl("/images/thumbnails/" + item.content_url + ".png");
            }
        }

        return firstPass;
    }

    public async Task ModerateIconAsync(ModerateIconRequest request, bool actorIsOwner)
    {
        var details = await db.QuerySingleOrDefaultAsync<AssetIconModerationRow>(
            "SELECT moderation_status, content_url, asset_id FROM asset_icon WHERE asset_icon.id = :id",
            new
            {
                id = request.iconId,
            });
        if (details == null)
            throw new StaffException("Asset ID is invalid");
        if (details.moderation_status != ModerationStatus.AwaitingApproval && !actorIsOwner)
        {
            throw new StaffException("You can only moderate items in a pending state. This item was already approved or declined.");
        }

        if (request.isApproved)
        {
            await db.ExecuteAsync("UPDATE asset_icon SET moderation_status = :status WHERE id = :id",
                new
                {
                    id = request.iconId,
                    status = ModerationStatus.ReviewApproved,
                });

            if (request.is18Plus)
            {
                await db.ExecuteAsync("UPDATE asset SET is_18_plus = true WHERE id = :id",
                    new
                    {
                        id = details.asset_id,
                    });
            }
        }
        else
        {
            await db.ExecuteAsync("UPDATE asset_icon SET moderation_status = :status WHERE id = :id",
                new
                {
                    status = ModerationStatus.Declined,
                    id = request.iconId,
                });

            if (!string.IsNullOrWhiteSpace(details.content_url))
            {
                await assets.DeleteAssetContent(details.content_url, Roblox.Configuration.ThumbnailsDirectory);
            }
        }
    }

    public async Task ToggleGroupIconAsync(IconToggleRequest request, long actorUserId)
    {
        request.name = NormalizeGroupIconName(request.name);

        var affected = await db.ExecuteAsync(
            "UPDATE group_icon SET is_approved = :approved WHERE group_id = :gid AND name = :name",
            new
            {
                request.approved,
                gid = request.groupId,
                request.name,
            });
        if (affected == 0)
        {
            throw new StaffException("The icon URL is no longer valid. Maybe the group owner created a new icon before the previous one was approved?");
        }

        await AwardCommissionForModerationAsync(actorUserId);

        if (request.approved == 2 && !string.IsNullOrWhiteSpace(request.name))
        {
            await assets.DeleteAssetContent(request.name, Roblox.Configuration.GroupIconsDirectory);
        }
    }

    public async Task<AdminGroupModerationInfoResponse> GetGroupModerationInfoAsync(long groupId)
    {
        var iconRaw = await db.QuerySingleOrDefaultAsync("SELECT * FROM group_icon WHERE group_id = :gid", new
        {
            gid = groupId,
        });
        var infoRaw = await db.QuerySingleOrDefaultAsync("SELECT * FROM \"group\" WHERE id = :gid", new
        {
            gid = groupId,
        });

        AdminDataRow? icon = iconRaw == null ? null : ToAdminRow((object)iconRaw);
        if (icon != null && icon.TryGetValue("name", out var iconName) && iconName != null)
            icon["name"] = GetImageUrl("/images/groups/" + iconName, false);

        return new AdminGroupModerationInfoResponse
        {
            icon = icon,
            info = infoRaw == null ? null : ToAdminRow((object)infoRaw),
        };
    }

    public async Task<AdminTotalResponse> GetUserJoinCountAsync(string period)
    {
        var t = DateTime.UtcNow.Subtract(period is "past-day" ? TimeSpan.FromDays(1) :
            period is "past-hour" ? TimeSpan.FromHours(1) :
            period is "past-week" ? TimeSpan.FromDays(7) : TimeSpan.FromDays(30));
        var all = await db.QuerySingleOrDefaultAsync<Total>(
            "SELECT COUNT(*) AS total FROM \"user\" WHERE created_at >= :t", new
            {
                t,
            });
        return new AdminTotalResponse { total = all.total };
    }

    public async Task<AdminUsersResponse> GetUsersAsync(string orderByColumn = "user.id", string? orderByMode = "asc",
        int limit = 10, int offset = 0, string? query = null, long? userId = null)
    {
        if (!whitelistedUserSorts.Contains(orderByColumn))
            throw new StaffException("Invalid sort column");
        if (orderByMode != "asc" && orderByMode != "desc")
            throw new StaffException("Invalid sort mode");
        if (limit is > 10000 or < 1) limit = 10;
        orderByColumn = orderByColumn.Replace("user_economy", "ue").Replace("user", "u");

        var sql = new SqlBuilder();
        var t = sql.AddTemplate(
            "SELECT u.id, u.username, u.description, u.created_at, u.online_at, u.status, u.is_18_plus, ja.id as join_application_id, ja.status as join_application_status, ui.id as invite_id, ui.author_id as invite_author_id, us.*, ue.* FROM \"user\" u LEFT JOIN user_settings us ON us.user_id = u.id LEFT JOIN user_economy ue on u.id = ue.user_id LEFT JOIN join_application ja on u.id = ja.user_id LEFT JOIN user_invite ui on u.id = ui.user_id /**where**/ /**orderby**/ LIMIT :limit OFFSET :offset", new { limit, offset });
        if (!string.IsNullOrWhiteSpace(query))
        {
            var normalizedQuery = query.Trim();
            sql.Where("u.username ILIKE :query", new { query = "%" + normalizedQuery + "%" });
            sql.OrderBy("CASE WHEN LOWER(u.username) = LOWER(:exactQuery) THEN 0 ELSE 1 END", new { exactQuery = normalizedQuery });
        }
        sql.OrderBy(orderByColumn + " " + orderByMode + " NULLS LAST");
        if (userId != null)
            sql.Where("u.id = :userId", new { userId });

        var rows = (await db.QueryAsync(t.RawSql, t.Parameters)).Select(ToAdminRow).ToList();
        foreach (var row in rows)
        {
            ConvertEnumField<AccountStatus>(row, "status");
            ConvertEnumField<TradeQualityFilter>(row, "trade_filter");
            ConvertEnumField<GeneralPrivacy>(row, "inventory_privacy");
            ConvertEnumField<GeneralPrivacy>(row, "trade_privacy");
            ConvertEnumField<GeneralPrivacy>(row, "private_message_privacy");
            ConvertEnumField<Gender>(row, "gender");
            ConvertEnumField<UserApplicationStatus>(row, "join_application_status");
            row["is_admin"] = false;
            row["is_moderator"] = false;
            row["password"] = "";
        }

        return new AdminUsersResponse { data = rows };
    }

    public async Task<AdminDataRow> GetUserInfoDetailedAsync(long userId, Func<long, bool> isOwnerUserId)
    {
        var raw = await db.QuerySingleOrDefaultAsync(
            @"SELECT u.id, u.verified, u.username, u.description, u.created_at, u.online_at, u.status, us.*, ue.*, avatar.thumbnail_url,
            ub.author_user_id as ban_author_user_id, ban_author.username as ban_author_username, ub.reason as ban_reason,
            ub.internal_reason as ban_reason_internal, ub.created_at as ban_created_at, ub.expired_at as ban_expired_at, ub.updated_at as ban_updated_at
            FROM ""user"" u
            LEFT JOIN user_settings us on u.id = us.user_id
            LEFT JOIN user_economy ue on u.id = ue.user_id
            LEFT JOIN user_avatar avatar ON avatar.user_id = u.id
            LEFT JOIN user_ban ub ON ub.user_id = u.id
            LEFT JOIN ""user"" as ban_author ON ban_author.id = ub.author_user_id
            WHERE u.id = :user_id
            LIMIT 1",
            new
            {
                user_id = userId,
            });
        if (raw == null)
            throw new StaffException("Invalid user ID");

        var result = ToAdminRow((object)raw);
        var joinInvite = await users.GetUserInvite(userId);
        var joinApp = await users.GetApplicationByUserId(userId);
        var membership = await users.GetUserMembership(userId);
        var year = await users.GetYear(userId);

        if (result.TryGetValue("thumbnail_url", out var thumbnailUrl) && thumbnailUrl != null)
            result["thumbnail_url"] = GetImageUrl(thumbnailUrl.ToString()!);
        ConvertEnumField<ThemeTypes>(result, "theme");
        ConvertEnumField<AccountStatus>(result, "status");
        ConvertEnumField<TradeQualityFilter>(result, "trade_filter");
        ConvertEnumField<GeneralPrivacy>(result, "inventory_privacy");
        ConvertEnumField<GeneralPrivacy>(result, "trade_privacy");
        ConvertEnumField<GeneralPrivacy>(result, "private_message_privacy");
        ConvertEnumField<Gender>(result, "gender");
        result["is_admin"] = false;
        result["is_moderator"] = await IsStaffAsync(userId, isOwnerUserId);
        result["membership"] = membership;
        result["invite"] = joinInvite;
        result["joinApp"] = joinApp;
        result["year"] = year.ToString();
        return result;
    }

    public async Task UnbanUserAsync(UserIdRequest request, AdminActorContext actor)
    {
        var status = await users.GetUserById(request.userId);
        if (status.accountStatus == AccountStatus.Forgotten)
            throw new StaffException("Forgotten accounts cannot be un-banned");

        await machineBan.UnbanAsync(request.userId, actor.userId);
    }

    public async Task BanUserAsync(BanUserRequest request, AdminActorContext actor, Func<long, bool> isOwnerUserId)
    {
        if (!await cooldown.TryIncrementBucketCooldown("BanUserV2:" + actor.userId, 60, TimeSpan.FromHours(1)))
            throw new StaffException("You are being rate limited, pleae try again later");

        DateTime? expirationDate = string.IsNullOrWhiteSpace(request.expires)
            ? null
            : DateTime.SpecifyKind(DateTime.Parse(request.expires), DateTimeKind.Utc);

        var doesExpire = expirationDate != null;
        if (request.isMachineBan && !actor.isOwner)
            throw new StaffException("Only an owner can machine ban users");
        if (request.isMachineBan && doesExpire)
            throw new StaffException("Machine bans must be permanent");
        if (request.isMachineBan && (string.IsNullOrWhiteSpace(request.internalReason) || request.internalReason.Length < 3))
            throw new StaffException("Internal reason is required for machine bans");

        var info = await users.GetUserById(request.userId);
        if (actor.userId == request.userId)
            throw new StaffException("You cannot ban yourself");
        if (info.accountStatus != AccountStatus.Ok && info.accountStatus != AccountStatus.Suppressed && info.accountStatus != AccountStatus.MustValidateEmail)
            throw new StaffException("You cannot ban this user. Current status is " + info.accountStatus);
        if (await IsStaffAsync(request.userId, isOwnerUserId) && !actor.isOwner)
            throw new StaffException("You cannot ban this user.");

        if (request.isMachineBan)
        {
            await machineBan.ActivateAsync(request.userId, actor.userId, request.internalReason);
            return;
        }

        if (!doesExpire)
        {
            await permanentTermination.TerminateAsync(
                request.userId,
                actor.userId,
                request.reason,
                request.internalReason);
            return;
        }

        await db.ExecuteAsync(
            "INSERT INTO user_ban (user_id, reason, author_user_id, expired_at, internal_reason) VALUES (:user_id, :reason, :author, :expires, :internal_reason)", new
            {
                internal_reason = request.internalReason,
                user_id = request.userId,
                request.reason,
                author = actor.userId,
                expires = expirationDate,
            });
        await db.ExecuteAsync("INSERT INTO moderation_ban (user_id, actor_id, reason, internal_reason, expired_at) VALUES (:user_id, :author, :reason, :internal_reason, :expires)", new
        {
            user_id = request.userId,
            author = actor.userId,
            reason = request.reason,
            internal_reason = request.internalReason,
            expires = expirationDate,
        });
        await db.ExecuteAsync("UPDATE \"user\" SET status = :st WHERE id = :id", new
        {
            st = AccountStatus.Suppressed,
            id = request.userId,
        });
        await users.InvalidateUserInfoCache(request.userId);
        await db.ExecuteAsync("UPDATE user_asset SET price = 0 WHERE price != 0 AND user_id = :user_id", new
        {
            user_id = request.userId,
        });
        await gameServer.KickPlayer(request.userId);
        await users.ExpireAllSessions(request.userId);
    }

    public async Task CreateMessageAsync(CreateMessageRequest request, AdminActorContext actor)
    {
        await users.GetUserById(request.userId);
        if (request.body.Length is > 1024 or < 1)
            throw new StaffException("Body is not valid");
        if (request.subject.Length is > 64 or < 1)
            throw new StaffException("Subject is not valid");
        await privateMessages.CreateMessage(request.userId, 1, request.subject, request.body);
        await db.ExecuteAsync("INSERT INTO moderation_admin_message(user_id, actor_id, body, subject) VALUES (:user_id, :actor_id, :body, :subject)", new
        {
            user_id = request.userId,
            actor_id = actor.userId,
            request.body,
            request.subject,
        });
    }

    public async Task<IReadOnlyCollection<AdminDataRow>> GetMessagesFromStaffAsync(long userId, int limit = 10, int offset = 0)
    {
        if (limit is > 100 or < 1) limit = 10;
        var rows = await db.QueryAsync(
            "SELECT user_message.* FROM user_message WHERE user_id_to = :id AND user_id_from = 1 ORDER BY id DESC LIMIT :limit OFFSET :offset",
            new
            {
                id = userId,
                limit,
                offset,
            });
        return ToAdminRows(rows);
    }

    public async Task NullifyUserPasswordAsync(UserIdRequest request, AdminActorContext actor, Func<long, bool> isOwnerUserId)
    {
        if (await IsStaffAsync(request.userId, isOwnerUserId) && !actor.isOwner)
            throw new StaffException("Bad user id");
        await db.ExecuteAsync("UPDATE \"user\" SET password = '' WHERE id = :id", new
        {
            id = request.userId,
        });
    }

    public Task DeleteAllSessionsAsync(UserIdRequest request)
    {
        return users.ExpireAllSessions(request.userId);
    }

    public async Task LockUserAsync(UserIdRequest request, AdminActorContext actor, Func<long, bool> isOwnerUserId)
    {
        if (await IsStaffAsync(request.userId, isOwnerUserId) && !actor.isOwner)
            throw new StaffException("Cannot lock this user");
        await db.ExecuteAsync("UPDATE \"user\" SET status = :status, session_expired_at = now() WHERE id = :id", new
        {
            id = request.userId,
            status = AccountStatus.MustValidateEmail,
        });
        await users.InvalidateUserInfoCache(request.userId);
    }

    public async Task RegenerateAvatarAsync(UserIdRequest request)
    {
        using var avatarCache = ServiceProvider.GetOrCreate<AvatarCache>();
        await avatarCache.DeleteAvatarCache(request.userId);
        await avatar.RedrawAvatar(request.userId, default, default, default, true, true);
    }

    public async Task ResetAvatarAsync(UserIdRequest request, Func<long, bool> isOwnerUserId)
    {
        if (await IsStaffAsync(request.userId, isOwnerUserId))
            throw new StaffException("Cannot reset avatar for this user");

        await avatar.RedrawAvatar(request.userId, new List<long>(), new ColorEntry
        {
            headColorId = 194,
            torsoColorId = 23,
            rightArmColorId = 194,
            leftArmColorId = 194,
            rightLegColorId = 102,
            leftLegColorId = 102,
        }, AvatarType.R6, true, true);

        using var avatarCache = ServiceProvider.GetOrCreate<AvatarCache>();
        avatarCache.UnscheduleRender(request.userId);
        await avatarCache.DeleteAvatarCache(request.userId);
    }

    public async Task<IReadOnlyCollection<AdminMacAddressHistoryEntry>> GetMacAddressHistoryAsync(long userId, AdminActorContext actor)
    {
        if (!actor.isOwner)
            throw new NotFoundException();
        var rawData = await users.GetMacAddresses(userId);

        var result = new List<AdminMacAddressHistoryEntry>();
        foreach (var item in rawData)
        {
            if (item == null)
                continue;
            result.Add(new AdminMacAddressHistoryEntry
            {
                userId = userId,
                macAddress = FormatMacAddress(item.macAddress),
                createdAt = item.createdAt,
                updatedAt = item.updatedAt,
            });
        }

        return result;
    }

    public async Task<IReadOnlyCollection<AdminAltAccountByMacEntry>> GetAltAccountsByMacAsync(AdminActorContext actor, int limit = 50, int offset = 0)
    {
        if (limit is > 200 or < 1) limit = 50;
        if (offset < 0) offset = 0;

        var macs = (await db.QueryAsync<MacAccountCountRow>(
            @"SELECT mac_address::text AS ""macAddress"", COUNT(DISTINCT user_id) AS ""userCount""
              FROM user_mac_address
              GROUP BY mac_address
              HAVING COUNT(DISTINCT user_id) > 1
              ORDER BY COUNT(DISTINCT user_id) DESC, mac_address ASC
              LIMIT @limit OFFSET @offset", new { limit, offset })).ToList();

        var result = new List<AdminAltAccountByMacEntry>();
        foreach (var mac in macs)
        {
            var userRows = await db.QueryAsync<AltAccountUserRow>(
                @"SELECT DISTINCT u.id, u.username, u.status
                  FROM user_mac_address ma
                  JOIN ""user"" u ON u.id = ma.user_id
                  WHERE ma.mac_address = @mac::macaddr
                  ORDER BY u.id ASC", new { mac = mac.macAddress });

            result.Add(new AdminAltAccountByMacEntry
            {
                macAddress = FormatMacAddress(mac.macAddress),
                userCount = mac.userCount,
                users = userRows.Select(u => new AdminAltAccountUserEntry
                {
                    id = u.id,
                    username = u.username,
                    status = u.status.ToString(),
                }).ToList(),
            });
        }

        return result;
    }

    public async Task<AdminUserAltAccountsResponse> GetUserAltAccountScoresAsync(AdminActorContext actor, long userId)
    {
        await users.GetUserById(userId);
        var rawSourceMacs = await db.QueryAsync<string>(
            "SELECT mac_address::text FROM user_mac_address WHERE user_id = @userId",
            new { userId });
        var sourceMacs = NormalizeEvidenceMacAddresses(rawSourceMacs);
        if (sourceMacs.Count == 0)
            return new AdminUserAltAccountsResponse();

        var candidateRows = (await db.QueryAsync<AltAccountEvidenceRow>(
            @"SELECT u.id, u.username, u.status,
                     array_agg(DISTINCT all_mac.mac_address::text ORDER BY all_mac.mac_address::text) AS ""macAddresses""
              FROM user_mac_address overlap_mac
              JOIN ""user"" u ON u.id = overlap_mac.user_id
              JOIN user_mac_address all_mac ON all_mac.user_id = u.id
              WHERE overlap_mac.mac_address::text = ANY(@sourceMacs)
                AND u.id != @userId
              GROUP BY u.id, u.username, u.status",
            new { sourceMacs = sourceMacs.Select(value => value.ToLowerInvariant()).ToArray(), userId })).ToList();

        var evidenceRows = candidateRows.Select(candidate =>
        {
            var candidateMacs = NormalizeEvidenceMacAddresses(candidate.macAddresses);
            var sharedMacCount = candidateMacs.Intersect(sourceMacs, StringComparer.OrdinalIgnoreCase).Count();
            var unionCount = candidateMacs.Union(sourceMacs, StringComparer.OrdinalIgnoreCase).Count();
            var exactMacSet = sourceMacs.SetEquals(candidateMacs);
            return new AltAccountEvidence(candidate, candidateMacs.Count, sharedMacCount, unionCount, exactMacSet);
        }).Where(evidence => evidence.sharedMacCount > 0).ToList();

        var sharedIpCounts = new Dictionary<long, int>();
        if (evidenceRows.Count != 0)
        {
            var ipRows = await db.QueryAsync<AltSharedIpRow>(
                @"SELECT candidate.user_id AS ""userId"",
                         COUNT(DISTINCT candidate.ip_hash)::int AS ""sharedIpHashCount""
                  FROM user_ip_address source
                  JOIN user_ip_address candidate ON candidate.ip_hash = source.ip_hash
                  WHERE source.user_id = @userId
                    AND candidate.user_id = ANY(@candidateIds)
                  GROUP BY candidate.user_id",
                new { userId, candidateIds = evidenceRows.Select(value => value.row.id).ToArray() });
            sharedIpCounts = ipRows.ToDictionary(value => value.userId, value => value.sharedIpHashCount);
        }

        var result = evidenceRows.Select(evidence =>
        {
            var sharedIpHashCount = sharedIpCounts.GetValueOrDefault(evidence.row.id);
            var similarity = evidence.unionCount == 0
                ? 0
                : (double)evidence.sharedMacCount / evidence.unionCount;
            return new AdminAltAccountScoreEntry
            {
                id = evidence.row.id,
                username = evidence.row.username,
                status = evidence.row.status.ToString(),
                score = CalculateAltAccountScore(evidence.exactMacSet, similarity, sharedIpHashCount),
                evidenceLevel = evidence.exactMacSet ? "Exact MAC set" : "Partial MAC overlap",
                exactMacSet = evidence.exactMacSet,
                sharedMacCount = evidence.sharedMacCount,
                candidateMacCount = evidence.candidateMacCount,
                sharedIpHashCount = sharedIpHashCount,
            };
        }).OrderByDescending(value => value.score).ThenBy(value => value.id).ToList();

        return new AdminUserAltAccountsResponse
        {
            sourceMacCount = sourceMacs.Count,
            data = result,
        };
    }

    public static int CalculateAltAccountScore(bool exactMacSet, double macSetSimilarity, int sharedIpHashCount)
    {
        if (!exactMacSet && macSetSimilarity <= 0)
            return 0;
        var macScore = exactMacSet
            ? 90
            : Math.Min(75, 35 + (int)Math.Round(Math.Clamp(macSetSimilarity, 0, 1) * 40));
        var ipBoost = Math.Min(10, Math.Max(0, sharedIpHashCount) * 5);
        return Math.Min(100, macScore + ipBoost);
    }

    private static HashSet<string> NormalizeEvidenceMacAddresses(IEnumerable<string> values)
    {
        var result = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var value in values)
        {
            try
            {
                var address = PhysicalAddress.Parse(value.Replace(":", string.Empty).Replace("-", string.Empty));
                if (!MachineBanService.IsExcludedMachineBanAddress(address))
                    result.Add(FormatMacAddress(address.ToString()));
            }
            catch (FormatException)
            {
            }
        }

        return result;
    }

    public async Task<IReadOnlyCollection<AdminIdentitySearchEntry>> SearchUsersByMacAddressAsync(
        AdminActorContext actor, string macAddress, bool exactSetOnly = false)
    {
        if (!actor.isOwner)
            throw new NotFoundException();

        string normalizedMac;
        try
        {
            var parsedMac = PhysicalAddress.Parse(macAddress.Trim().Replace(":", string.Empty).Replace("-", string.Empty));
            if (parsedMac.GetAddressBytes().Length != 6)
                throw new FormatException();
            normalizedMac = FormatMacAddress(parsedMac.ToString());
        }
        catch (Exception exception) when (exception is FormatException or ArgumentException)
        {
            throw new StaffException("Invalid MAC address");
        }

        var rows = (await db.QueryAsync<MacIdentitySearchRow>(
            @"SELECT u.id, u.username, u.status,
                     array_agg(DISTINCT all_mac.mac_address::text ORDER BY all_mac.mac_address::text) AS ""macAddresses""
              FROM user_mac_address matched
              JOIN ""user"" u ON u.id = matched.user_id
              JOIN user_mac_address all_mac ON all_mac.user_id = u.id
              WHERE matched.mac_address = @macAddress::macaddr
              GROUP BY u.id, u.username, u.status
              ORDER BY u.id ASC", new { macAddress = normalizedMac })).ToList();

        if (exactSetOnly)
        {
            rows = rows
                .GroupBy(row => string.Join('|', row.macAddresses.Select(FormatMacAddress)
                    .OrderBy(value => value, StringComparer.OrdinalIgnoreCase)), StringComparer.OrdinalIgnoreCase)
                .Where(group => group.Count() > 1)
                .SelectMany(group => group)
                .OrderBy(row => row.id)
                .ToList();
        }

        return rows.Select(row => new AdminIdentitySearchEntry
        {
            id = row.id,
            username = row.username,
            status = row.status.ToString(),
            macAddresses = row.macAddresses.Select(FormatMacAddress)
                .OrderBy(value => value, StringComparer.OrdinalIgnoreCase)
                .ToList(),
        }).ToList();
    }

    public async Task<IReadOnlyCollection<AdminIdentitySearchEntry>> SearchUsersByIpHashAsync(
        AdminActorContext actor, string ipHash)
    {
        if (!actor.isOwner)
            throw new NotFoundException();
        var normalizedHash = NormalizeIpHash(ipHash);

        var rows = await db.QueryAsync<IpIdentitySearchRow>(
            @"SELECT u.id, u.username, u.status,
                     array_agg(DISTINCT ip.action ORDER BY ip.action) AS actions,
                     MIN(ip.created_at) AS ""createdAt"", MAX(ip.updated_at) AS ""updatedAt""
              FROM user_ip_address ip
              JOIN ""user"" u ON u.id = ip.user_id
              WHERE ip.ip_hash = @ipHash
              GROUP BY u.id, u.username, u.status
              ORDER BY u.id ASC", new { ipHash = normalizedHash });

        return rows.Select(row => new AdminIdentitySearchEntry
        {
            id = row.id,
            username = row.username,
            status = row.status.ToString(),
            actions = row.actions,
            createdAt = row.createdAt,
            updatedAt = row.updatedAt,
        }).ToList();
    }

    public async Task<AdminIpBanStatusResponse> GetIpBanStatusAsync(AdminActorContext actor, string ipHash)
    {
        if (!actor.isOwner)
            throw new NotFoundException();
        var normalizedHash = NormalizeIpHash(ipHash);
        var isBanned = await db.QuerySingleOrDefaultAsync<bool>(
            "SELECT EXISTS(SELECT 1 FROM user_ip_ban WHERE ip_hash = @ipHash AND revoked_at IS NULL)",
            new { ipHash = normalizedHash });
        return new AdminIpBanStatusResponse { ipHash = normalizedHash, isBanned = isBanned };
    }

    public async Task SetIpBanAsync(AdminActorContext actor, AdminIpBanRequest request)
    {
        if (!actor.isOwner)
            throw new NotFoundException();
        var normalizedHash = NormalizeIpHash(request.ipHash);
        var internalReason = request.internalReason.Trim();
        if (internalReason.Length is < 3 or > 4096)
            throw new StaffException("Internal reason must contain between 3 and 4096 characters");

        await db.ExecuteAsync(
            @"INSERT INTO user_ip_ban (ip_hash, actor_user_id, internal_reason)
              VALUES (@ipHash, @actorUserId, @internalReason)
              ON CONFLICT (ip_hash) DO UPDATE SET
                  actor_user_id = EXCLUDED.actor_user_id,
                  internal_reason = EXCLUDED.internal_reason,
                  revoked_at = NULL,
                  updated_at = now()", new
            {
                ipHash = normalizedHash,
                actorUserId = actor.userId,
                internalReason,
            });
    }

    public async Task RevokeIpBanAsync(AdminActorContext actor, string ipHash)
    {
        if (!actor.isOwner)
            throw new NotFoundException();
        var normalizedHash = NormalizeIpHash(ipHash);
        await db.ExecuteAsync(
            "UPDATE user_ip_ban SET revoked_at = now(), updated_at = now() WHERE ip_hash = @ipHash AND revoked_at IS NULL",
            new { ipHash = normalizedHash });
    }

    public async Task<IReadOnlyCollection<AdminUserBanHistoryEntry>> GetUserBanHistoryAsync(long userId)
    {
        var rawData = await db.QueryAsync<ModerationBanHistoryRow>("SELECT * FROM moderation_ban WHERE user_id = :user_id ORDER BY id DESC LIMIT 1000", new
        {
            user_id = userId,
        });

        var result = new List<AdminUserBanHistoryEntry>();
        foreach (var item in rawData)
        {
            result.Add(new AdminUserBanHistoryEntry
            {
                id = item.id,
                user_id = item.user_id,
                reason = item.reason ?? "No reason provided",
                internal_reason = item.internal_reason ?? "No internal reason provided ",
                created_at = item.created_at.ToString("yyyy-MM-dd HH:mm:ss"),
                expired_at = item.expired_at?.ToString("yyyy-MM-dd HH:mm:ss"),
                actor_id = item.actor_id,
                actor_username = (await users.GetUserById(item.actor_id)).username,
            });
        }

        return result;
    }

    public async Task<IReadOnlyCollection<AdminDataRow>> GetUserStatusHistoryAsync(long userId)
    {
        var rows = await db.QueryAsync("SELECT * FROM user_status WHERE user_id = :user_id AND status IS NOT NULL ORDER BY id DESC", new
        {
            user_id = userId,
        });
        return ToAdminRows(rows);
    }

    public async Task<IReadOnlyCollection<AdminDataRow>> GetUserCommentHistoryAsync(long userId)
    {
        var rows = await db.QueryAsync("SELECT * FROM asset_comment WHERE user_id = :user_id ORDER BY id DESC LIMIT 1000",
            new
            {
                user_id = userId,
            });
        return ToAdminRows(rows);
    }

    public Task DeleteUserStatusAsync(long userId, long statusId)
    {
        return db.ExecuteAsync("UPDATE user_status SET status = '[ Content Deleted ]' WHERE id = :id AND user_id = :user_id", new
        {
            id = statusId,
            user_id = userId,
        });
    }

    public async Task RefundTransactionAsync(long transactionId, long assetId, long expectedAmount, long userId, AdminActorContext actor)
    {
        var transaction = await db.QuerySingleOrDefaultAsync<RefundTransactionEntry>(
            "SELECT id, asset_id as assetId, amount, user_id_one as userId, user_id_two as otherUserId, user_asset_id as userAssetId, currency_type as currencyType FROM user_transaction WHERE id = :id", new
            {
                id = transactionId,
            });
        if (transaction == null)
            throw new StaffException("Transaction does not exist");
        if (transaction.userId != userId || transaction.assetId != assetId || transaction.amount != expectedAmount)
            throw new StaffException("Transaction state is not valid. Reload the page and try again");

        if (transaction.userAssetId != 0 && transaction.userAssetId != null)
        {
            try
            {
                var userAsset = await users.GetUserAssetById(transaction.userAssetId.Value);
                if (userAsset == null || userAsset.userId != userId)
                    throw new StaffException("User asset does not exist or is no longer owned by this user");
            }
            catch (RecordNotFoundException)
            {
                throw new StaffException("User asset no longer exists");
            }
        }

        var wornAssets = await avatar.GetWornAssets(userId);
        var shouldUpdateAvatar = wornAssets.Any(a => a == assetId);

        await db.ExecuteAsync("INSERT INTO moderation_refund_transaction(actor_id, user_id_one, user_id_two, asset_id, user_asset_id, amount, currency_type, transaction_id) VALUES(:actor_id, :user_id_one, :user_id_two, :asset_id, :user_asset_id, :amount, :currency_type, :transaction_id)", new
        {
            actor_id = actor.userId,
            user_id_one = userId,
            user_id_two = transaction.otherUserId,
            asset_id = assetId,
            user_asset_id = transaction.userAssetId,
            amount = expectedAmount,
            currency_type = transaction.currencyType,
            transaction_id = transactionId,
        });
        await economy.IncrementCurrency(CreatorType.User, userId, transaction.currencyType, transaction.amount);
        var badDecisionsUserId = await users.GetUserIdFromUsername("BadDecisions");
        if (transaction.userAssetId != 0 && transaction.userAssetId != null)
        {
            await db.ExecuteAsync("UPDATE user_asset SET user_id = :bd WHERE id = :id", new
            {
                id = transaction.userAssetId.Value,
                bd = badDecisionsUserId,
            });
        }
        else
        {
            await db.ExecuteAsync("UPDATE user_asset SET user_id = :bd WHERE asset_id = :asset_id AND user_id = :user_id", new
            {
                asset_id = transaction.assetId,
                user_id = transaction.userId,
                bd = badDecisionsUserId,
            });
        }

        await db.ExecuteAsync("DELETE FROM user_transaction WHERE id = :id", new
        {
            id = transactionId,
        });

        if (shouldUpdateAvatar)
        {
            Writer.Info(LogGroup.AdminApi, "refunded transaction {0}. userId {1} requires a redraw", transaction.id, userId);
            await avatar.RedrawAvatar(userId, default, default, default, default, true);
        }
    }

    public async Task<IReadOnlyCollection<AdminDataRow>> GetAssetProductHistoryAsync(long assetId)
    {
        var rows = await db.QueryAsync(
            "SELECT p.id, p.asset_id, a.name, p.actor_id, u.username, p.is_for_sale, price_in_tickets, price_in_robux, p.is_limited, p.is_limited_unique, p.max_copies, p.offsale_at, p.created_at FROM moderation_update_product p LEFT JOIN asset a ON a.id = asset_id LEFT JOIN \"user\" u ON u.id = p.actor_id WHERE p.asset_id = :asset_id ORDER BY id DESC", new
            {
                asset_id = assetId,
            });
        return ToAdminRows(rows);
    }

    public async Task<IReadOnlyCollection<AdminDataRow>> GetSaleHistoryAsync(long assetId, int limit, int offset, DateTime? start = null, DateTime? end = null)
    {
        var qb = new SqlBuilder();
        var t = qb.AddTemplate("SELECT t.id, t.user_id_one, u.username, t.amount, t.currency_type, t.user_asset_id, t.created_at FROM user_transaction t INNER JOIN \"user\" u ON u.id = t.user_id_one /**where**/ ORDER BY id DESC LIMIT :limit OFFSET :offset", new
        {
            limit,
            offset,
        });
        qb.Where("t.user_id_two = 1 AND t.type = :type AND t.sub_type = :sub_type AND t.asset_id = :asset_id", new
        {
            type = PurchaseType.Purchase,
            sub_type = TransactionSubType.ItemPurchase,
            asset_id = assetId,
        });
        if (start != null)
            qb.Where("t.created_at >= :start", new { start = start.Value });
        if (end != null)
            qb.Where("t.created_at <= :end", new { end = end.Value });
        var rows = await db.QueryAsync(t.RawSql, t.Parameters);
        return ToAdminRows(rows);
    }

    public async Task<AdminModerationLogsResponse> GetModerationLogsAsync(string logType, int limit = 10, int offset = 0,
        bool descending = true, string? author = null, string? actioned = null)
    {
        if (limit is > 100 or < 1) limit = 10;
        logType = logType.ToLower();

        var sql = new SqlBuilder();
        SqlBuilder.Template template;
        string[] columns;

        switch (logType)
        {
            case "ban":
                template = sql.AddTemplate(@"
                    SELECT 
                        mb.id, mb.created_at, mb.expired_at, mb.reason, 
                        mb.internal_reason, mb.user_id, actioned.username, mb.actor_id, 
                        author.username as author_username 
                    FROM moderation_ban mb
                        INNER JOIN ""user"" actioned ON actioned.id = mb.user_id 
                        INNER JOIN ""user"" author ON author.id = mb.actor_id 
                    /**where**/ /**orderby**/
                    LIMIT :limit OFFSET :offset
                    ", new { limit, offset });
                columns = new[] { "#", "Date", "Expires", "Reason", "Internal Reason", "UserID", "Username", "AuthorID", "Author Username" };
                break;
            case "unban":
                template = sql.AddTemplate(@"
                    SELECT 
                        mb.id, mb.created_at, mb.user_id, 
                        actioned.username, mb.actor_id, author.username as author_username 
                    FROM moderation_unban mb
                        INNER JOIN ""user"" actioned ON actioned.id = mb.user_id 
                        INNER JOIN ""user"" author ON author.id = mb.actor_id 
                    /**where**/ /**orderby**/
                    LIMIT :limit OFFSET :offset
                    ", new { limit, offset });
                columns = new[] { "#", "Date", "UserID", "Username", "AuthorID", "Author Username" };
                break;
            case "item":
                template = sql.AddTemplate(@"
                    SELECT 
                        mb.id, mb.created_at, mb.user_asset_id, ua.asset_id, mb.user_id, 
                        actioned.username, mb.author_user_id, author.username as author_username
                    FROM moderation_give_item mb
                        INNER JOIN ""user"" actioned ON actioned.id = mb.user_id 
                        INNER JOIN ""user"" author ON author.id = mb.author_user_id 
                        INNER JOIN ""user_asset"" ua ON ua.id = mb.user_asset_id
                    /**where**/ /**orderby**/
                    LIMIT :limit OFFSET :offset
                    ", new { limit, offset });
                columns = new[] { "#", "Date", "UserAssetID", "AssetID", "UserID", "Username", "Author ID", "Author Username" };
                break;
            case "asset":
                template = sql.AddTemplate(@"
                    SELECT 
                        mb.id, asset_id, actor_id, author.username as author_username, action, mb.created_at
                    FROM moderation_manage_asset mb
                        INNER JOIN ""user"" author ON author.id = mb.actor_id 
                    /**where**/ /**orderby**/
                    LIMIT :limit OFFSET :offset
                    ", new { limit, offset });
                columns = new[] { "#", "Asset ID", "Author ID", "Author Username", "Status", "Date" };
                break;
            case "alert":
                template = sql.AddTemplate(@"
                    SELECT 
                        mb.id, alert, alert_url, actor_id, author.username as author_username, mb.created_at
                    FROM moderation_set_alert mb
                        INNER JOIN ""user"" author ON author.id = mb.actor_id 
                    /**where**/ /**orderby**/
                    LIMIT :limit OFFSET :offset
                    ", new { limit, offset });
                columns = new[] { "#", "Text", "URL", "Author ID", "Author Username", "Date" };
                break;
            case "message":
                template = sql.AddTemplate(@"
                    SELECT 
                        mb.id, subject, body, actor_id, user_id,
                        author.username as author_username, actioned.username, mb.created_at
                    FROM moderation_admin_message mb
                        INNER JOIN ""user"" author ON author.id = mb.actor_id 
                        INNER JOIN ""user"" actioned ON actioned.id = mb.user_id 
                    /**where**/ /**orderby**/
                    LIMIT :limit OFFSET :offset
                    ", new { limit, offset });
                columns = new[] { "#", "Subject", "Body", "Author ID", "User ID", "Author Username", "Messaged Username", "Date" };
                break;
            case "applications":
                template = sql.AddTemplate(@"
                    SELECT 
                        mb.id, application_id, author_user_id, new_status,
                        author.username as author_username, mb.created_at
                    FROM moderation_change_join_app mb
                        INNER JOIN ""user"" author ON author.id = mb.author_user_id 
                    /**where**/ /**orderby**/
                    LIMIT :limit OFFSET :offset
                    ", new { limit, offset });
                columns = new[] { "#", "Application ID", "Author ID", "New Status", "Author Username", "Date" };
                break;
            case "refund":
                template = sql.AddTemplate(@"
                    SELECT 
                        mb.id, asset_id, actor_id, user_id_one, amount,
                        currency_type, user_asset_id, author.username as author_username, mb.created_at
                    FROM moderation_refund_transaction mb
                        INNER JOIN ""user"" author ON author.id = mb.actor_id 
                    /**where**/ /**orderby**/
                    LIMIT :limit OFFSET :offset
                    ", new { limit, offset });
                columns = new[] { "#", "Asset ID", "Author ID", "UserID", "Amount", "Currency", "UAID", "Date" };
                break;
            case "product":
                template = sql.AddTemplate(@"
                    SELECT 
                        mb.id, mb.asset_id, a.name, mb.actor_id, mb.is_for_sale, price_in_tickets,
                        price_in_robux, mb.is_limited, mb.is_limited_unique, mb.max_copies,
                        mb.offsale_at, author.username as author_username, mb.created_at 
                    FROM moderation_update_product mb 
                        LEFT JOIN asset a ON a.id = asset_id
                        INNER JOIN ""user"" author ON author.id = mb.actor_id
                    /**where**/ /**orderby**/
                    LIMIT :limit OFFSET :offset
                    ", new { limit, offset });
                columns = new[] { "#", "Asset ID", "Name", "Author ID", "IsForSale", "Price (R$)", "Price (T$)", "Limited", "LimitedU", "MaxCopies", "Offsale", "Author Username", "Date" };
                break;
            case "trade":
            case "trade-rollback":
                template = sql.AddTemplate(@"
                    SELECT
                        mb.id, mb.trade_id, mb.actor_id, author.username as author_username,
                        mb.user_id_one, user_one.username as user_one_username,
                        mb.user_id_two, user_two.username as user_two_username,
                        mb.created_at
                    FROM moderation_rollback_trade mb
                        INNER JOIN ""user"" author ON author.id = mb.actor_id
                        INNER JOIN ""user"" user_one ON user_one.id = mb.user_id_one
                        INNER JOIN ""user"" user_two ON user_two.id = mb.user_id_two
                    /**where**/ /**orderby**/
                    LIMIT :limit OFFSET :offset
                    ", new { limit, offset });
                columns = new[] { "#", "Trade ID", "Author ID", "Author Username", "User One ID", "User One Username", "User Two ID", "User Two Username", "Date" };
                break;
            case "robux":
            case "tickets":
                var table = logType == "robux" ? "moderation_give_robux" : "moderation_give_tickets";
                template = sql.AddTemplate(@"
                    SELECT 
                        mb.id, mb.created_at, mb.amount, mb.user_id, 
                        mb.author_user_id, author.username as author_username
                    FROM " + table + @" mb
                        INNER JOIN ""user"" author ON author.id = mb.author_user_id
                    /**where**/ /**orderby**/
                    LIMIT :limit OFFSET :offset
                    ", new { limit, offset });
                columns = new[] { "#", "Date", logType == "robux" ? "Robux Amount" : "Tix Amount", "User ID", "Author ID", "Author Username" };
                break;
            default:
                throw new StaffException("Bad log type " + logType);
        }

        if (!string.IsNullOrWhiteSpace(actioned) && DoesActionHaveActioned(logType))
            sql.Where("actioned.username ILIKE :actioned", new { actioned });
        if (!string.IsNullOrWhiteSpace(author))
            sql.Where("author.username ILIKE :author", new { author });

        sql.OrderBy($"mb.id {(descending ? "DESC" : "ASC")}");

        var result = (await db.QueryAsync(template.RawSql, template.Parameters)).Select(ToAdminRow).ToList();
        if (logType.Equals("applications"))
        {
            foreach (var row in result)
                ConvertEnumField<UserApplicationStatus>(row, "new_status");
        }
        else if (logType.Equals("asset"))
        {
            foreach (var row in result)
                ConvertEnumField<ModerationStatus>(row, "action");
        }

        return new AdminModerationLogsResponse
        {
            data = result,
            columns = columns,
        };
    }

    public Task<IEnumerable<Roblox.Dto.Users.BadgeEntry>> GetUserBadgesAsync(long userId)
    {
        return accountInformation.GetUserBadges(userId);
    }

    public async Task GiveUserBadgeAsync(GiveBadgeRequest request, AdminActorContext actor, Func<long, bool> isOwnerUserId)
    {
        if (await IsStaffAsync(request.userId, isOwnerUserId) && !actor.isOwner)
            throw new StaffException("Cannot modify badges for this user");
        var ent = BadgesMetadata.Badges.Find(v => v.id == request.badgeId);
        if (ent == null)
            throw new StaffException("BadgeId does not exist");
        await db.ExecuteAsync("INSERT INTO user_badge (user_id, badge_id) VALUES (:user_id, :badge_id)", new
        {
            user_id = request.userId,
            badge_id = request.badgeId,
        });
    }

    public async Task DeleteUserBadgeAsync(GiveBadgeRequest request, AdminActorContext actor, Func<long, bool> isOwnerUserId)
    {
        if (await IsStaffAsync(request.userId, isOwnerUserId) && !actor.isOwner)
            throw new StaffException("Cannot modify badges for this user");
        await db.ExecuteAsync("DELETE FROM user_badge WHERE user_id = :user_id AND badge_id = :badge_id", new
        {
            user_id = request.userId,
            badge_id = request.badgeId,
        });
    }

    public async Task GiveUserTicketsAsync(GiveUserTicketsRequest request, AdminActorContext actor)
    {
        if (request.tickets is <= -10000000 or > 10000000)
            throw new StaffException("Invalid ticket amount. Must be between 1 and 10M (inclusive)");

        await db.ExecuteAsync("UPDATE user_economy SET balance_tickets = balance_tickets + :amt WHERE user_id = :user_id",
            new
            {
                user_id = request.userId,
                amt = request.tickets,
            });
        await db.ExecuteAsync(
            "INSERT INTO moderation_give_tickets (user_id, author_user_id, amount) VALUES (:user_id, :author_user_id, :amount)",
            new
            {
                user_id = request.userId,
                author_user_id = actor.userId,
                amount = request.tickets,
            });
    }

    public async Task GiveUserRobuxAsync(GiveUserRobuxRequest request, AdminActorContext actor)
    {
        if (request.robux is <= -10000000 or > 10000000)
            throw new StaffException("Invalid robux amount. Must be between 1 and 10M (inclusive)");

        await db.ExecuteAsync("UPDATE user_economy SET balance_robux = balance_robux + :amt WHERE user_id = :user_id",
            new
            {
                user_id = request.userId,
                amt = request.robux,
            });
        await db.ExecuteAsync(
            "INSERT INTO moderation_give_robux (user_id, author_user_id, amount) VALUES (:user_id, :author_user_id, :amount)",
            new
            {
                user_id = request.userId,
                author_user_id = actor.userId,
                amount = request.robux,
            });
    }

    public async Task<IReadOnlyCollection<AdminDataRow>> GetUserCollectiblesAsync(long userId)
    {
        var rows = await db.QueryAsync("SELECT asset_id, user_asset.id as user_asset_id, asset.name FROM user_asset INNER JOIN asset ON asset.id = user_asset.asset_id WHERE user_asset.user_id = :user_id AND (asset.is_limited = true OR asset.is_limited_unique = true)",
            new { user_id = userId });
        return ToAdminRows(rows);
    }

    public async Task RemoveItemAsync(RemoveItemRequest request, AdminActorContext actor)
    {
        RequireOwner(actor, "Cannot give remove items from this user");
        var transferTo = await users.GetUserIdFromUsername("BadDecisions");
        var affected = await db.ExecuteAsync(
            "UPDATE user_asset SET price = 0, user_id = :new_user_id, updated_at = now() WHERE user_id = :old_user_id AND user_asset.id = :user_asset_id",
            new
            {
                new_user_id = transferTo,
                old_user_id = request.userId,
                user_asset_id = request.userAssetId,
            });
        if (affected != 1)
            throw new StaffException("User asset is no longer owned by this user");
        await db.ExecuteAsync(
            "INSERT INTO moderation_give_item (user_id, author_user_id, user_asset_id, user_id_from) VALUES (:user_id, :author_user_id, :user_asset_id, :user_id_from)",
            new
            {
                user_id = transferTo,
                author_user_id = actor.userId,
                user_asset_id = request.userAssetId,
                user_id_from = request.userId,
            });
    }

    public async Task<IReadOnlyCollection<StaffUserAssetTrackEntry>> GetGiveItemCircAsync(long assetId, int limit)
    {
        var transferTo = await users.GetUserIdFromUsername("BadDecisions");
        return (await db.QueryAsync<StaffUserAssetTrackEntry>(
            "SELECT user_asset.id, user_asset.asset_id as assetId, user_asset.user_id as userId, u.username, user_asset.serial FROM user_asset INNER JOIN \"user\" u ON u.id = user_asset.user_id INNER JOIN \"user_ban\" ub ON ub.user_id = user_asset.user_id WHERE user_asset.asset_id = :asset_id AND ((u.status = :status AND ub.created_at <= :time_sub) OR u.id = :bad) AND user_asset.user_id != 1 ORDER BY user_asset.serial DESC NULLS LAST LIMIT :limit",
            new
            {
                bad = transferTo,
                status = AccountStatus.Deleted,
                time_sub = DateTime.UtcNow.Subtract(TimeSpan.FromDays(31)),
                asset_id = assetId,
                limit,
            })).ToArray();
    }

    public async Task GiveItemAsync(GiveItemRequest request, AdminActorContext actor)
    {
        var details = await assets.GetAssetCatalogInfo(request.assetId);
        if (!details.itemRestrictions.Contains("LimitedUnique") && request.giveSerial)
            throw new StaffException("This asset is not limited unique, cannot give serial");

        var terminatedCopies = (await GetGiveItemCircAsync(request.assetId, request.copies)).ToList();
        if (terminatedCopies.Count < request.copies)
        {
            for (var i = 0; i < (request.copies - terminatedCopies.Count); i++)
            {
                var saleCount = await assets.GetSaleCount(request.assetId);
                long? serial = request.giveSerial ? saleCount + 1 : null;
                var id = await db.QuerySingleOrDefaultAsync(
                    "INSERT INTO user_asset (asset_id, user_id, serial) VALUES (:asset_id, :user_id, :serial) RETURNING user_asset.id",
                    new
                    {
                        asset_id = request.assetId,
                        user_id = request.userId,
                        serial,
                    });
                await db.ExecuteAsync(
                    "INSERT INTO moderation_give_item (user_id, author_user_id, user_asset_id, user_id_from) VALUES (:user_id, :author_user_id, :user_asset_id, null)",
                    new
                    {
                        user_id = request.userId,
                        user_asset_id = (long)id.id,
                        author_user_id = actor.userId,
                    });

                if (serial != null)
                {
                    await economy.InsertTransaction(new AssetPurchaseTransaction(request.userId,
                        details.creatorType, details.creatorTargetId, CurrencyType.Robux, 0, request.assetId, (long)id.id));
                    await assets.IncrementSaleCount(request.assetId);
                }
            }
        }

        foreach (var item in terminatedCopies)
        {
            await db.ExecuteAsync("UPDATE user_asset SET user_id = :uid, updated_at = now(), price = 0 WHERE id = :id", new
            {
                id = item.id,
                uid = request.userId,
            });
            await db.ExecuteAsync(
                "INSERT INTO moderation_give_item (user_id, author_user_id, user_asset_id, user_id_from) VALUES (:user_id, :author_user_id, :user_asset_id, :user_id_from)",
                new
                {
                    user_id = request.userId,
                    author_user_id = actor.userId,
                    user_asset_id = item.id,
                    user_id_from = item.userId,
                });
        }
    }

    public async Task<IReadOnlyCollection<AdminTrackedItemHistoryEntry>> TrackItemAsync(long userAssetId)
    {
        var saleData = await economy.GetTransactionsForUserAssetId(userAssetId);
        var tradeData = await trades.GetTradesByUserAssetId(userAssetId);

        var historyList = new List<AdminTrackedItemHistoryEntry>();
        historyList.AddRange(saleData.Select(item => new AdminTrackedItemHistoryEntry
        {
            created_at = item.createdAt,
            track_type = "Sale",
            user_id_two = item.userIdTwo,
            user_id_one = item.userIdOne,
            user_one_username = item.userNameOne,
            user_two_username = item.userNameTwo,
            amount = item.amount,
            currency_type = (int)item.currency,
        }));
        historyList.AddRange(tradeData.Select(item => new AdminTrackedItemHistoryEntry
        {
            created_at = item.createdAt,
            track_type = "Trade",
            user_id_two = item.userIdTwo,
            user_id_one = item.userIdOne,
            user_one_username = item.usernameOne,
            user_two_username = item.usernameTwo,
            id = item.id,
        }));

        return historyList;
    }

    public async Task DeleteUserAsync(UserIdRequest request, Func<long, bool> isOwnerUserId)
    {
        if (await IsStaffAsync(request.userId, isOwnerUserId))
            throw new StaffException("Cannot delete this user");
        var key = "staff:userdeletion:v1";
        if ((await redis.StringGetAsync(key)) != null)
            throw new StaffException("An account deletion was already requested recently. Try again in about 10 seconds.");
        await redis.StringSetAsync(key, "{}", TimeSpan.FromSeconds(10));

        await users.DeleteUser(request.userId, true);
        await ResetAvatarAsync(request, isOwnerUserId);
    }

    public async Task<IEnumerable<string>> GetPreviousUsernamesAsync(long userId)
    {
        return (await users.GetPreviousUsernames(userId)).Select(c => c.username);
    }

    public async Task DeleteUsernameAsync(DeleteUsernameRequest request, AdminActorContext actor, Func<long, bool> isOwnerUserId)
    {
        RequireOwner(actor, "InternalServerError");
        if (await IsStaffAsync(request.userId, isOwnerUserId) && !actor.isOwner)
            throw new StaffException("Cannot modify this user's usernames");
        var previousNames = (await users.GetPreviousUsernames(request.userId)).ToList();
        var totalChanges = previousNames.Where(c => c.username.ToLower() == request.username.ToLower()).ToList();
        if (totalChanges.Count == 0)
            throw new StaffException("The username provided has not been used by this user.");

        await users.InTransaction(async _ =>
        {
            var usersDb = users.db;
            await usersDb.ExecuteAsync("DELETE FROM user_previous_username WHERE user_id = :id AND username ILIKE :name", new
            {
                id = request.userId,
                name = request.username,
            });

            await privateMessages.CreateMessage(request.userId, 1,
                "Username Refund", @$"Hello,

We have deleted one of your previous usernames, ""{request.username}"". You will no longer have access to this username.

Thank you for your understanding,


-The Averia Team");
            return 0;
        });
    }

    public Task DeleteCommentAsync(long userId, long commentId)
    {
        return db.ExecuteAsync("UPDATE asset_comment SET comment = '[ Content Deleted ]' WHERE id = :id AND user_id = :user_id", new
        {
            id = commentId,
            user_id = userId,
        });
    }

    public async Task DeleteForumPostAsync(DeleteForumPostRequest request)
    {
        var details = await db.QuerySingleOrDefaultAsync("SELECT id, thread_id FROM forum_post WHERE id = :id", new
        {
            id = request.postId,
        });
        if (details == null)
            throw new StaffException("Post does not exist");
        if (details.thread_id == null)
            await db.ExecuteAsync("DELETE FROM forum_post WHERE id = :id OR thread_id = :id", new { id = request.postId });
        else
            await db.ExecuteAsync("UPDATE forum_post SET post = '[ Content Deleted ]' WHERE id = :id", new { id = request.postId });
    }

    public Task LockForumThreadAsync(long threadId)
    {
        return db.ExecuteAsync("UPDATE forum_post SET is_locked = true WHERE id = :id AND thread_id IS NULL", new
        {
            id = threadId,
        });
    }

    public async Task<AdminLotteryRunResponse> RunLotteryAsync(AdminActorContext actor)
    {
        var log = Writer.CreateWithId(LogGroup.Lottery);
        log.Info("Lottery start. Initiated by {0}", actor.userId);

        var allItems = (await GetLotteryItemsAsync()).ToList();
        log.Info("There are {0} items available", allItems.Count);
        if (allItems.Count == 0)
            throw new StaffException("There are no items available for lottery");
        var allUsers = (await GetEligibleLotteryUsersAsync()).ToList();
        log.Info("There are {0} users available", allUsers.Count);
#if !DEBUG
        if (allUsers.Count < 10)
            throw new StaffException("At least 10 users have to be online to run the lottery");
#endif
        var randomItem = allItems[new Random().Next(0, allItems.Count)];
        log.Info("Picked item. UAID = {0} Old Owner = {1}", randomItem.userAssetId, randomItem.userId);
        var randomUser = allUsers[new Random().Next(0, allUsers.Count)];
        log.Info("Picked user. ID = {0}", randomUser.userId);
        await db.ExecuteAsync("UPDATE user_asset SET user_id = :user_id, updated_at = now(), price = 0 WHERE id = :id", new
        {
            user_id = randomUser.userId,
            id = randomItem.userAssetId,
        });
        log.Info("item {0} transferred from {1} to {2}", randomItem.userAssetId, randomItem.userId, randomUser.userId);
        await privateMessages.CreateMessage(randomUser.userId, 1, "You Won The Lottery!",
            "Congrats! Your account was chosen as the winner for today's lottery, where a Limited or Limited Unique item is given away after the owner has been offline for 6 months or more.\n\nThe item you won is: " +
            randomItem.name + ", which has a Recent Average Price of " + randomItem.recentAveragePrice + ". The item has already been added to your account - no action is required to claim it.\nIf you do not want this item, you can sell it on the market or trade it with another user for an item you do want.\n\n-The Averia Team");
        log.Info("sent message to user picked {0}", randomUser.userId);
        await privateMessages.CreateMessage(randomItem.userId, 1, "Inactive Account Penalty",
            "Hello\n\nAs part of our efforts to encourage activity and discourage account compromises, we have removed the item " +
            randomItem.name +
            " from your inventory, and awarded it to a random player who was active at the time of our lottery draw. We understand that you may not have been expecting this to happen, however, it is outlined in our policy that we reserve the right to remove items from accounts once they've been inactive for 6 months or longer. At the time of sending this message, your account has been inactive since " + randomUser.onlineAt.ToString("MMMM dd, yyyy") + "\n\nItems taken from your account for lottery purposes cannot be restored. We hope you understand,\n\n-The Averia Team");
        log.Info("sent message to old asset owner {0}", randomItem.userId);
        return new AdminLotteryRunResponse
        {
            name = randomItem.name,
            username = randomUser.username,
        };
    }

    public async Task<IEnumerable<UserLotteryEntry>> GetEligibleLotteryUsersAsync()
    {
        return await db.QueryAsync<UserLotteryEntry>(
            "SELECT u.username, u.id as userId, u.online_at as onlineAt FROM \"user\" u WHERE u.online_at >= :online_time AND u.created_at <= :creation_time AND u.status = :status", new
            {
                status = AccountStatus.Ok,
                online_time = DateTime.UtcNow.Subtract(TimeSpan.FromMinutes(10)),
                creation_time = DateTime.UtcNow.Subtract(TimeSpan.FromDays(30)),
            });
    }

    public async Task<IEnumerable<LotteryItemEntry>> GetLotteryItemsAsync()
    {
        return await db.QueryAsync<LotteryItemEntry>("SELECT a.name, a.id as assetId, a.recent_average_price as recentAveragePrice, u.id as userId, u.online_at as onlineAt, u.username, ua.id as userAssetId FROM user_asset ua INNER JOIN \"user\" u on u.id = ua.user_id INNER JOIN \"asset\" a ON a.id = ua.asset_id WHERE u.id != 1 AND u.id != 12 AND u.online_at <= :time AND (a.is_limited OR a.is_limited_unique) AND NOT a.is_for_sale AND u.status = :status ORDER BY u.online_at LIMIT 1000", new
        {
            status = AccountStatus.Ok,
            time = DateTime.UtcNow.Subtract(TimeSpan.FromDays(30)),
        });
    }

    public Dictionary<int, string> GetAssetTypes()
    {
        return Enum.GetValues<Type>().ToDictionary(value => (int)value, value => value.ToString());
    }

    public Dictionary<int, string> GetAssetGenres()
    {
        return Enum.GetValues<Genre>().ToDictionary(value => (int)value, value => value.ToString());
    }

    public async Task RequestAssetReRenderAsync(ReRenderRequest request)
    {
        var details = await assets.GetAssetCatalogInfo(request.assetId);
        await assets.RenderAssetsInBurstAsync(new[] { (request.assetId, details.assetType) });
    }

    private sealed class BuggedRenderAssetRow
    {
        public long assetId { get; set; }
        public Type assetType { get; set; }
    }

    public async Task<FixBuggedRendersResponse> FixBuggedRendersAsync(FixBuggedRendersRequest request)
    {
        var limit = Math.Clamp(request.limit ?? 100, 1, 500);
        var sortDirection = request.newestFirst ? "DESC" : "ASC";
        var robloxOwned = request.ownership == "roblox";
        var rows = (await db.QueryAsync<BuggedRenderAssetRow>(
            @"SELECT asset.id AS ""assetId"", asset.asset_type AS ""assetType""
              FROM asset
              LEFT JOIN asset_thumbnail thumbnail ON thumbnail.asset_id = asset.id
              WHERE asset.moderation_status = :approvedStatus
                AND asset.creator_type = :creatorType
                AND (
                    (:robloxOwned AND asset.creator_id = 1)
                    OR (NOT :robloxOwned AND asset.creator_id <> 1)
                )
                AND asset.asset_type <> ALL(:excludedTypes)
                AND (
                    thumbnail.asset_id IS NULL
                    OR thumbnail.content_url IS NULL
                    OR thumbnail.moderation_status <> :approvedStatus
                )
              ORDER BY asset.id " + sortDirection + @"
              LIMIT :limit",
            new
            {
                approvedStatus = (int)ModerationStatus.ReviewApproved,
                creatorType = (int)CreatorType.User,
                robloxOwned,
                excludedTypes = new[] { (int)Type.Audio, (int)Type.Video },
                limit,
            })).ToList();

        await assets.RenderAssetsInBurstAsync(rows.Select(row => (row.assetId, row.assetType)));

        return new FixBuggedRendersResponse
        {
            matchedCount = rows.Count,
            rerenderedAssetIds = rows.Select(row => row.assetId).ToArray(),
        };
    }

    public async Task<AdminAssetDetailsResponse> GetAssetDetailsAsync(long assetId)
    {
        var devInfo = await assets.MultiGetAssetDeveloperDetails(new[] { assetId });
        var info = await assets.MultiGetInfoById(new[] { assetId });
        return new AdminAssetDetailsResponse
        {
            developerInfo = devInfo,
            info = info,
        };
    }

    public Task<ProductEntry> GetProductDetailsAsync(long assetId)
    {
        return assets.GetProductForAsset(assetId);
    }

    private async Task InsertProductLog(long assetId, long userId, bool isLimited, bool isLimitedUnique, DateTime? offsaleAt, int? maxCopies, long? priceRobux, long? priceTickets, bool isForSale)
    {
        await db.ExecuteAsync("INSERT INTO moderation_update_product (asset_id, actor_id, is_limited, is_limited_unique, offsale_at, max_copies, price_in_robux, price_in_tickets, is_for_sale) VALUES (@asset_id, @actor_id, @is_limited, @is_limited_unique, @offsale_at, @max_copies, @robux, @tix, @is_for_sale)", new
        {
            asset_id = assetId,
            actor_id = userId,
            is_limited = isLimited,
            is_limited_unique = isLimitedUnique,
            offsale_at = offsaleAt,
            max_copies = maxCopies,
            robux = priceRobux,
            tix = priceTickets,
            is_for_sale = isForSale,
        });
    }

    public async Task UpdateAssetProductAsync(UpdateProductRequest request, AdminActorContext actor)
    {
        var details = await assets.GetProductForAsset(request.assetId);

        if (!actor.isOwner)
        {
            if (!HasPermission(actor, Access.MakeItemLimited))
            {
                if (details.isLimited || details.isLimitedUnique)
                    throw new StaffException("You do not have permission to update a limited item");

                request.isLimited = false;
                request.isLimitedUnique = false;
                request.maxCopies = null;
            }

            var extraInfo = await assets.GetAssetCatalogInfo(request.assetId);
            if (extraInfo.creatorType != CreatorType.User && extraInfo.creatorTargetId != 1)
                throw new StaffException("You do not have permission to update a product that is not owned by the admin account");
        }

        var existingLog = await db.QuerySingleOrDefaultAsync<Total>("SELECT count(*) as total FROM moderation_update_product WHERE asset_id = :asset_id", new
        {
            asset_id = request.assetId,
        });
        if (existingLog.total == 0)
            await InsertProductLog(request.assetId, 1, details.isLimited, details.isLimitedUnique, details.offsaleAt, details.serialCount, details.priceRobux, details.priceTickets, details.isForSale);

        await InsertProductLog(request.assetId, actor.userId, request.isLimited, request.isLimitedUnique, request.offsaleDeadline?.ToUniversalTime(), request.maxCopies, request.priceRobux, request.priceTickets, request.isForSale);

        await assets.UpdateAssetMarketInfoName(request.assetId, request.assetName);
        await assets.UpdateAssetMarketDescriptionInfo(request.assetId, request.description);
        await assets.UpdateAssetMarketInfo(request.assetId, request.isForSale, request.isLimited, request.isLimitedUnique, request.maxCopies, request.offsaleDeadline?.ToUniversalTime());
        await assets.SetItemPrice(request.assetId, request.priceRobux, request.priceTickets);
    }

    public async Task StartAssetSaleAsync(StartSaleRequest request, AdminActorContext actor)
    {
        RequireOwner(actor, "Only the owner can start sales");
        await assets.StartSale(request.assetId, request.pctOff, request.flatRobux, request.flatTix, request.salesUnits);
    }

    public async Task EndAssetSaleAsync(EndSaleRequest request, AdminActorContext actor)
    {
        RequireOwner(actor, "Only the owner can end sales");
        await assets.EndSale(request.assetId);
    }

    public async Task<AdminMessageResponse> GroupVerifyAsync(long groupId, bool verify, AdminActorContext actor)
    {
        RequireOwner(actor, "Not authorized to verify groups");
        await db.ExecuteAsync("UPDATE \"group\" SET verified = :isVerified WHERE id = :id", new
        {
            isVerified = verify,
            id = groupId,
        });
        return new AdminMessageResponse { message = "Group " + groupId + " has been " + (verify ? "verified" : "unverified") + "." };
    }

    public async Task<AdminMessageResponse> CreatePromocodeAsync(string promocode, int? robux, long? assetId, AdminActorContext actor)
    {
        RequireOwner(actor, "Not authorized to create promocodes");
        try
        {
            await promocodes.AddPromocode(promocode, robux, assetId);
            return new AdminMessageResponse { message = "Created promocode" };
        }
        catch (Exception e)
        {
            return new AdminMessageResponse { message = "Failed to create promocode: " + e.Message };
        }
    }

    public async Task<AdminMessageResponse> DeletePromocodeAsync(string promocode, AdminActorContext actor)
    {
        RequireOwner(actor, "Not authorized to create promocodes");
        try
        {
            await promocodes.DeletePromocode(promocode);
            return new AdminMessageResponse { message = "Deleted promocode" };
        }
        catch (Exception e)
        {
            return new AdminMessageResponse { message = "Failed to delete promocode: " + e.Message };
        }
    }

    public async Task<AdminCreateGameResponse> CreateGameAsync(UserIdRequest request)
    {
        var username = (await users.GetUserById(request.userId)).username;
        var asset = await assets.CreatePlace(request.userId, username, CreatorType.User, request.userId);
        var universe = await games.CreateUniverse(asset.placeId);
        return new AdminCreateGameResponse
        {
            placeId = asset.placeId,
            universeId = universe.universeId,
        };
    }

    public async Task<AssetVersionWithIdEntry> CreateAssetVersionAsync(CreateAssetVersionRequest request, AdminActorContext actor)
    {
        if (request.rbxm == null)
            throw new StaffException("No file specified");

        var info = await assets.GetAssetCatalogInfo(request.assetId);
        var canUpload = info.creatorType is CreatorType.User && info.creatorTargetId == 1 ||
                        await assets.CanUserModifyItem(info.id, actor.userId);
        if (!canUpload && !actor.isOwner)
            throw new StaffException("Not authorized to modify this item");
        if (info.assetType == Type.Package)
            throw new StaffException("Cannot create an asset version for this type");
        var result = await assets.CreateAssetVersion(request.assetId, 1, request.rbxm.OpenReadStream());
        await assets.RenderAssetsInBurstAsync(new[] { (request.assetId, info.assetType) });
        return new AssetVersionWithIdEntry
        {
            assetId = result.assetId,
        };
    }

    public IReadOnlyDictionary<FeatureFlag, bool> GetAllFlags()
    {
        return FeatureFlags.GetAllFlags();
    }

    public Task EnableFlagAsync(string featureFlag)
    {
        return FeatureFlags.EnableFlag(Enum.Parse<FeatureFlag>(featureFlag));
    }

    public Task DisableFlagAsync(string featureFlag)
    {
        return FeatureFlags.DisableFlag(Enum.Parse<FeatureFlag>(featureFlag));
    }

    public async Task<IReadOnlyCollection<AdminDataRow>> GetInGamePlayersAsync()
    {
        var rows = await db.QueryAsync("SELECT s.user_id, s.asset_id, s.server_id, u.username, a.name as asset_name FROM asset_server_player s INNER JOIN \"user\" u ON u.id = s.user_id INNER JOIN asset a ON a.id = s.asset_id LIMIT 1000");
        return ToAdminRows(rows);
    }

    public async Task<AdminTotalResponse> GetOnlinePlayersCountAsync()
    {
        var t = DateTime.UtcNow.Subtract(TimeSpan.FromSeconds(60));
        var count = await db.QuerySingleOrDefaultAsync<Total>("SELECT COUNT(*) as total FROM \"user\" WHERE online_at >= :t", new { t });
        return new AdminTotalResponse { total = count.total };
    }

    public Task<IEnumerable<TransactionEntryDb>> GetUserTransactionsAsync(long userId, PurchaseType type, int offset, int limit)
    {
        return economy.GetTransactions(userId, CreatorType.User, type, limit, offset);
    }

    public Task<IEnumerable<TransactionEntryDb>> GetAllUserTransactionsAsync(long userId, int offset, int limit)
    {
        return economy.GetTransactions(userId, CreatorType.User, limit, offset);
    }

    public async Task<IReadOnlyCollection<AdminTradeHistoryResponse>> GetUserTradesAsync(long userId, TradeType type, int offset, int limit)
    {
        var response = new List<AdminTradeHistoryResponse>();
        var result = await trades.GetTradesOfType(userId, type, limit, offset);
        foreach (var item in result)
        {
            response.Add(new AdminTradeHistoryResponse
            {
                trade = item,
                db = await trades.GetTradeById(item.id),
                items = await trades.GetTradeItems(item.id),
            });
        }

        return response;
    }

    public async Task RollbackTradeAsync(long tradeId, AdminActorContext actor)
    {
        var trade = await trades.GetTradeById(tradeId);
        await trades.RollbackTrade(tradeId);
        await db.ExecuteAsync(
            "INSERT INTO moderation_rollback_trade (trade_id, actor_id, user_id_one, user_id_two) VALUES (:trade_id, :actor_id, :user_id_one, :user_id_two)",
            new
            {
                trade_id = tradeId,
                actor_id = actor.userId,
                user_id_one = trade.userIdOne,
                user_id_two = trade.userIdTwo,
            });
        Writer.Info(LogGroup.AdminApi, "Admin user {0} rolled back trade {1} between users {2} and {3}", actor.userId, tradeId, trade.userIdOne, trade.userIdTwo);
    }

    public async Task<IEnumerable<PendingUgcRequestEntry>> GetPendingUgcRequestsAsync()
    {
        return await db.QueryAsync<PendingUgcRequestEntry>(
            "SELECT ur.id, ur.user_id as userId, ur.roblox_asset_id as robloxAssetId, ur.roblox_url as robloxUrl, ur.item_name as itemName, ur.created_at as createdAt, u.username as creatorName " +
            "FROM ugc_request ur INNER JOIN \"user\" u ON u.id = ur.user_id " +
            "WHERE ur.status = :status ORDER BY ur.id ASC LIMIT 50",
            new { status = (short)Roblox.Models.UgcRequest.UgcRequestStatus.Pending });
    }

    public async Task<AdminSuccessResponse> ModerateUgcRequestAsync(ModerateUgcRequestBody request, AdminActorContext actor)
    {
        var newStatus = request.isApproved
            ? (short)Roblox.Models.UgcRequest.UgcRequestStatus.Approved
            : (short)Roblox.Models.UgcRequest.UgcRequestStatus.Declined;

        var row = await db.QuerySingleOrDefaultAsync<PendingUgcRequestEntry>(
            "UPDATE ugc_request SET status = :newStatus, decided_at = NOW(), decided_by = :by " +
            "WHERE id = :id AND status = :pending " +
            "RETURNING id, user_id as userId, roblox_asset_id as robloxAssetId, roblox_url as robloxUrl, item_name as itemName, status",
            new
            {
                newStatus,
                by = actor.userId,
                id = request.id,
                pending = (short)Roblox.Models.UgcRequest.UgcRequestStatus.Pending,
            });
        if (row == null)
            throw new StaffException("Request not found or already decided");

        if (request.isApproved)
        {
            long createdAssetId;
            var itemName = row.itemName ?? "your item";
            try
            {
                var copyResult = await CopyAssetFromRobloxAsync(new CopyAssetRequest
                {
                    assetId = row.robloxAssetId,
                    force = false,
                }, actor);
                createdAssetId = copyResult.assetId;
            }
            catch (Exception e)
            {
                await db.ExecuteAsync(
                    "UPDATE ugc_request SET status = :pending, decided_at = NULL, decided_by = NULL WHERE id = :id",
                    new { pending = (short)Roblox.Models.UgcRequest.UgcRequestStatus.Pending, id = row.id });
                if (e is StaffException) throw;
                throw new StaffException("Failed to copy item: " + e.Message);
            }

            await db.ExecuteAsync(
                "UPDATE ugc_request SET created_asset_id = :assetId WHERE id = :id",
                new { assetId = createdAssetId, id = row.id });

            var body = $"Good news! Your UGC item request was approved.\n\n" +
                       $"Item: {itemName}\n" +
                       $"Original URL: {row.robloxUrl}\n" +
                       $"View on Averia: /catalog/{createdAssetId}/--\n\n" +
                       $"Thanks for contributing!";
            await privateMessages.CreateMessage(row.userId, 1, "Your UGC item request was approved", body);
        }
        else
        {
            var body = $"Your UGC item request was declined.\n\n" +
                       $"Item URL: {row.robloxUrl}\n\n" +
                       $"You may submit a different item if you'd like.";
            await privateMessages.CreateMessage(row.userId, 1, "Your UGC item request was declined", body);
        }

        return new AdminSuccessResponse { success = true };
    }

    public async Task<CreateResponse> CreateAssetAsync(CreateAssetRequest request)
    {
        if (request.isLimitedUnique) request.isLimited = true;
        if (!Enum.IsDefined(request.assetTypeId))
            throw new StaffException("Bad assetTypeId");
        var isPackage = request.assetTypeId == Type.Package;
        var disableRender = isPackage;
        IEnumerable<long>? packageAssetIds = null;

        if (!isPackage && request.rbxm == null)
            throw new StaffException("No file specified");

        if (isPackage)
        {
            if (request.packageAssetIds == null)
                throw new StaffException("Must specify assetIds when creating a package");
            packageAssetIds = request.packageAssetIds.Split(",").Select(long.Parse);
            var packages = (await assets.MultiGetAssetDeveloperDetails(packageAssetIds)).ToList();
            var result = new Dictionary<Type, int>();
            foreach (var item in packages)
            {
                var type = (Type)item.typeId;
                result.TryAdd(type, 0);
                result[type]++;
            }

            var optionalOneOf = new List<Type>
            {
                Type.LeftArm, Type.LeftLeg, Type.RightLeg, Type.RightArm, Type.Torso, Type.Head, Type.Gear,
                Type.Shirt, Type.Pants, Type.Face, Type.RunAnimation, Type.IdleAnimation, Type.WalkAnimation,
                Type.FallAnimation, Type.ClimbAnimation, Type.JumpAnimation, Type.SwimAnimation, Type.EmoteAnimation
            };
            var optionalCanHaveMoreThanOne = new List<Type> { Type.Hat, Type.HairAccessory, Type.ShoulderAccessory, Type.BackAccessory, Type.FrontAccessory, Type.WaistAccessory, Type.NeckAccessory };
            foreach (var type in optionalOneOf)
            {
                if (result.ContainsKey(type) && result[type] > 1)
                    throw new StaffException("Package has too many of this type: " + type);
            }

            packageAssetIds = packages.Where(c =>
            {
                var t = (Type)c.typeId;
                return optionalOneOf.Contains(t) || optionalCanHaveMoreThanOne.Contains(t);
            }).Select(c => c.assetId);
        }

        Stream? file = null;
        if (request.rbxm != null)
        {
            var fileData = request.rbxm.OpenReadStream();
            if (request.assetTypeId != Type.Audio && request.assetTypeId != Type.EmoteAnimation && request.assetTypeId != Type.Image && request.assetTypeId != Type.Mesh && request.assetTypeId != Type.GamePass && request.assetTypeId != Type.Badge)
            {
                var isOk = await assets.RobloxFileValidation(fileData);
                if (!isOk)
                    throw new StaffException("The asset file doesn't look correct. Please try again.");
            }
            fileData.Position = 0;
            file = fileData;
        }

        var assetDetails = await assets.CreateAsset(request.name, request.description, 1,
            CreatorType.User, 1, file, request.assetTypeId, request.genre, ModerationStatus.ReviewApproved,
            DateTime.UtcNow, DateTime.UtcNow, request.robloxAssetId, disableRender,
            renderInBurst: true);
        if (request.assetTypeId == Type.Package)
        {
            if (packageAssetIds == null)
                throw new StaffException("packageAssetIds cannot be null when creating a package");

            foreach (var id in packageAssetIds.Distinct())
                await assets.InsertPackageAsset(assetDetails.assetId, id);
            await assets.RenderAssetsInBurstAsync(new[] { (assetDetails.assetId, request.assetTypeId) });
        }

        await assets.SetItemPrice(assetDetails.assetId, request.price, null);
        await assets.UpdateAssetMarketInfo(assetDetails.assetId, request.isForSale, request.isLimited,
            request.isLimitedUnique, request.maxCopies, request.offsaleDeadline?.ToUniversalTime());

        return assetDetails;
    }

    public async Task<CreateResponse> CreateClothingAssetAsync(CreateClothingRequest request)
    {
        if (request.file == null)
            throw new StaffException("No file specified");

        var buf = request.file.OpenReadStream();
        var ok = await assets.ValidateClothing(buf, request.assetTypeId);
        if (ok == null) throw new StaffException("Invalid file provided");
        buf.Position = 0;
        var texture = await assets.CreateAsset(request.file.FileName, $"{request.assetTypeId} Image", 1,
            CreatorType.User, 1, buf, Type.Image, Genre.All, ModerationStatus.ReviewApproved, DateTime.UtcNow,
            DateTime.UtcNow, renderInBurst: true);
        var asset = await assets.CreateAsset(request.name, request.description, 1, CreatorType.User, 1,
            null, request.assetTypeId, request.genre, ModerationStatus.ReviewApproved, DateTime.UtcNow,
            DateTime.UtcNow, request.robloxAssetId, false, texture.assetId, renderInBurst: true);
        await assets.SetItemPrice(asset.assetId, request.price, null);
        await assets.UpdateAssetMarketInfo(asset.assetId, request.isForSale, false, false, null, null);
        return asset;
    }

    public async Task<MigrateItemResponse> MigrateAnyItemFromRobloxAsync(MigrateItemAlternateRequest request, AdminActorContext actor)
    {
        FeatureFlags.FeatureCheck(FeatureFlag.UploadContentEnabled);
        var assetId = Roblox.Libraries.Assets.UrlUtilities.GetAssetIdFromUrl(request.url);
        var existing = await TryGetMigratedItemAsync(assetId);
        if (existing != null)
            return existing;

        await using var migrationLock = await Cache.redLock.CreateLockAsync(
            "MigrateItemFromRobloxV1:" + assetId,
            TimeSpan.FromSeconds(30));
        if (!migrationLock.IsAcquired)
            throw new LockNotAcquiredException();

        existing = await TryGetMigratedItemAsync(assetId);
        if (existing != null)
            return existing;

        var robloxDetails = await robloxApi.GetProductInfoAssetDelivery(assetId);
        Stream? content;
        long? contentId = null;
        if (robloxDetails.AssetTypeId == Type.Audio)
        {
            content = await robloxApi.GetAssetAudioContent(assetId);
        }
        else
        {
            if (robloxDetails is ProductInfoWithAssetDelivery extended)
            {
                if (string.IsNullOrEmpty(extended.location))
                    throw new StaffException("Roblox did not return a URL for this asset. Is the ID correct?");
                content = await robloxApi.GetStreamAsync(extended.location);
            }
            else
            {
                content = await robloxApi.GetAssetContent(assetId);
            }

            if (robloxDetails.AssetTypeId is Type.TShirt or Type.Shirt or Type.Pants)
            {
                var reader = new StreamReader(content);
                var templateContent = await reader.ReadToEndAsync();
                content.Position = 0;

                var robloxUrls = migrateItemAssetIdUrlRegex.Match(templateContent);
                if (!robloxUrls.Success)
                    throw new StaffException("Could not match for robloxUrl");

                contentId = long.Parse(robloxUrls.Groups[1].Value);
            }
        }

        var disableRender = request.disableRender;
#if DEBUG
        disableRender = true;
#endif
        var modState = robloxDetails.AssetTypeId is Type.Animation or Type.SolidModel or Type.Lua or Type.Mesh or Type.MeshPart or Type.Model
            ? ModerationStatus.ReviewApproved
            : ModerationStatus.AwaitingApproval;

        if (contentId != null)
        {
            var imageData = await robloxApi.GetAssetContent((long)contentId);
            if (robloxDetails.AssetTypeId == null)
                throw new StaffException("Null " + nameof(robloxDetails.AssetTypeId));

            var ok = await assets.ValidateClothing(imageData, robloxDetails.AssetTypeId.Value);
            if (ok == null)
                throw new StaffException("ValidateClothing() returned false");

            if (robloxDetails.Name == null)
                throw new StaffException("Null " + nameof(robloxDetails.Name));

            imageData.Position = 0;
            var shirtResult = await assets.CreateAsset(
                robloxDetails.Name,
                null,
                2,
                CreatorType.User,
                2,
                imageData,
                Type.Image,
                Genre.All,
                modState,
                DateTime.UtcNow,
                DateTime.UtcNow,
                contentId,
                disableRender,
                renderInBurst: true);

            imageData.Position = 0;
            var img = await Imager.ReadAsync(content);
            imageData.Position = 0;
            await assets.InsertOrUpdateAssetVersionMetadataImage(
                shirtResult.assetVersionId,
                (int)imageData.Length,
                img.width,
                img.height,
                img.imageFormat,
                await assets.GenerateImageHash(imageData));

            contentId = shirtResult.assetId;
            content = null;
        }

        if (robloxDetails.Name == null)
            throw new StaffException("Null " + nameof(robloxDetails.Name));
        if (robloxDetails.AssetTypeId == null)
            throw new StaffException("Null " + nameof(robloxDetails.AssetTypeId));

        var assetResult = await assets.CreateAsset(
            robloxDetails.Name,
            robloxDetails.Description,
            2,
            CreatorType.User,
            2,
            content,
            robloxDetails.AssetTypeId.Value,
            Genre.All,
            modState,
            robloxDetails.Created,
            robloxDetails.Updated,
            assetId,
            disableRender,
            contentId,
            assetIdOverride: assetId,
            renderInBurst: true);

        if (robloxDetails.AssetTypeId.Value == Type.Image && content != null)
        {
            content.Position = 0;
            var img = await Imager.ReadAsync(content);
            content.Position = 0;
            await assets.InsertOrUpdateAssetVersionMetadataImage(
                assetResult.assetVersionId,
                (int)content.Length,
                img.width,
                img.height,
                img.imageFormat,
                await assets.GenerateImageHash(content));
        }

        await db.ExecuteAsync(
            "INSERT INTO moderation_migrate_asset(asset_id, roblox_asset_id, actor_id) VALUES (@assetId, @robloxAssetId, @actorId)",
            new
            {
                assetResult.assetId,
                robloxAssetId = assetId,
                actorId = actor.userId,
            });

        return new MigrateItemResponse
        {
            assetId = assetResult.assetId,
            assetVersionId = assetResult.assetVersionId,
        };
    }

    private async Task<MigrateItemResponse?> TryGetMigratedItemAsync(long robloxAssetId)
    {
        try
        {
            var assetId = await assets.GetAssetIdFromRobloxAssetId(robloxAssetId);
            var latestVersion = await assets.GetLatestAssetVersion(assetId);
            return new MigrateItemResponse
            {
                assetId = assetId,
                assetVersionId = latestVersion.assetVersionId,
            };
        }
        catch (RecordNotFoundException)
        {
            return null;
        }
    }

    private async Task CopyItemFloodCheck(AdminActorContext actor)
    {
        if (!actor.isOwner)
        {
            var canUploadLocal = await cooldown.TryIncrementBucketCooldown(
                "CopyItemFromRobloxV1:" + actor.userId, 30, TimeSpan.FromHours(1));
            if (!canUploadLocal)
                throw new StaffException("Flood check reached for asset uploads on your account (hour). Try again in an hour");

            var canUploadLocalDay = await cooldown.TryIncrementBucketCooldown(
                "CopyItemFromRobloxV1:" + actor.userId, 40, TimeSpan.FromHours(12));
            if (!canUploadLocalDay)
                throw new StaffException("Flood check reached for asset uploads on your account (day). Try again tomorrow");

            var canUploadGlobal = await cooldown.TryIncrementBucketCooldown("CopyItemFromRobloxGlobalV1", 60,
                TimeSpan.FromHours(12));
            if (!canUploadGlobal)
                throw new StaffException("Global flood check reached for item uploads");
        }
    }

    public async Task<CreateResponse> CopyBundleAsync(long bundleId, AdminActorContext actor)
    {
        var details = await robloxApi.GetBundle(bundleId);
        if (details.bundleType != "BodyParts" && details.bundleType != "AvatarAnimations")
            throw new StaffException("Invalid bundleType " + details.bundleType);

        var alreadyExists = await assets.SearchCatalog(new CatalogSearchRequest
        {
            limit = 10,
            include18Plus = true,
            includeNotForSale = true,
            creatorType = CreatorType.User,
            creatorTargetId = 1,
            keyword = details.name,
        });
        if (alreadyExists._total > 0 && alreadyExists != null)
        {
            var existing = await assets.MultiGetInfoById(alreadyExists.data.Select(c => c.id));
            foreach (var ent in existing)
            {
                if (ent.assetType == Type.Package && ent.name == details.name)
                    throw new StaffException("It looks like this bundle already exists: AssetID=" + ent.id);
            }
        }

        var ids = new List<long>();
        foreach (var item in details.items)
        {
            if (item.type != "Asset") continue;
            var info = await robloxApi.GetProductInfo(item.id, false);
            var content = details.bundleType == "AvatarAnimations"
                ? await robloxApi.GetAssetContentFromProxy(item.id, 1)
                : await robloxApi.GetAssetContentFromProxy(item.id);
            content.Position = 0;
            var assetDetails = await assets.CreateAsset(item.name, null, 1,
                CreatorType.User, 1, content, info.AssetTypeId!.Value, Genre.All, ModerationStatus.ReviewApproved,
                DateTime.UtcNow, DateTime.UtcNow, item.id, renderInBurst: true);
            ids.Add(assetDetails.assetId);
        }

        await CopyItemFloodCheck(actor);
        return await CreateAssetAsync(new CreateAssetRequest
        {
            assetTypeId = Type.Package,
            description = details.description,
            genre = Genre.All,
            isForSale = false,
            isLimited = false,
            isLimitedUnique = false,
            maxCopies = null,
            name = details.name,
            offsaleDeadline = null,
            packageAssetIds = string.Join(",", ids.Select(c => c.ToString())),
        });
    }

    public async Task<BulkCopyAssetResponse> BackportAssetsFromRobloxAsync(BulkCopyAssetRequest request, AdminActorContext actor)
    {
        return await CopyRobloxAssetsInBulkAsync(request, actor, BulkRobloxCopyMode.UgcBackport);
    }

    public async Task<BulkCopyAssetResponse> CopyAssetsFromRobloxAsync(BulkCopyAssetRequest request, AdminActorContext actor)
    {
        return await CopyRobloxAssetsInBulkAsync(request, actor, BulkRobloxCopyMode.Asset);
    }

    private async Task<BulkCopyAssetResponse> CopyRobloxAssetsInBulkAsync(
        BulkCopyAssetRequest request,
        AdminActorContext actor,
        BulkRobloxCopyMode mode)
    {
        var assetIds = request.assetIds
            .Where(assetId => assetId > 0)
            .Distinct()
            .ToList();

        switch (assetIds.Count)
        {
            case 0:
                throw new StaffException("At least one Roblox asset ID is required");
            case > MaxBulkRobloxAssetCopyCount when !actor.isOwner:
                throw new StaffException($"Bulk copy is limited to {MaxBulkRobloxAssetCopyCount} assets at a time");
        }

        var resultByRobloxAssetId = new Dictionary<long, BulkCopyAssetResult>();
        var candidates = new List<BulkRobloxCopyCandidate>();
        var existingAssetIds = request.force
            ? new Dictionary<long, long>()
            : await GetExistingCopiedRobloxAssetIds(assetIds);
        var catalogDetails = await TryGetCatalogItemDetailsBatchAsync(assetIds.Where(assetId => !existingAssetIds.ContainsKey(assetId)));

        foreach (var assetId in assetIds)
        {
            try
            {
                if (existingAssetIds.TryGetValue(assetId, out var existingAssetId))
                {
                    resultByRobloxAssetId[assetId] = CreateBulkCopySuccess(assetId, existingAssetId, null, true);
                    continue;
                }

                var details = await GetBulkRobloxProductDetailsAsync(assetId, catalogDetails);
                var skipReason = GetBulkCopySkipReason(details, request);
                if (skipReason != null)
                {
                    resultByRobloxAssetId[assetId] = CreateBulkCopySkipped(assetId, skipReason);
                    continue;
                }

                ValidateRobloxCopyDetails(details, GetBulkCopyAllowedTypes(mode), actor, !request.keepLimitedProperties);
                if (!request.force)
                    await EnsureNoRobloxCopyDuplicate(details);

                var priceRobux = ApplyRobloxCopyDiscountPercent(GetRobloxCopyPrice(details, request), request.discountedPricePercent);
                candidates.Add(new BulkRobloxCopyCandidate(assetId, details, priceRobux));
            }
            catch (Exception ex)
            {
                resultByRobloxAssetId[assetId] = new BulkCopyAssetResult
                {
                    robloxAssetId = assetId,
                    success = false,
                    error = GetBulkCopyErrorMessage(ex),
                };
            }
        }

        if (mode == BulkRobloxCopyMode.Asset)
            await CopyBulkRobloxAssetsAsync(candidates, request, actor, resultByRobloxAssetId);
        else
            await BackportBulkRobloxAssetsAsync(candidates, request, actor, resultByRobloxAssetId);

        var results = assetIds
            .Where(resultByRobloxAssetId.ContainsKey)
            .Select(assetId => resultByRobloxAssetId[assetId])
            .ToList();

        return new BulkCopyAssetResponse
        {
            results = results,
            catalogUrls = results
                .Where(result => result.success && !string.IsNullOrWhiteSpace(result.catalogUrl))
                .Select(result => result.catalogUrl!)
                .ToList(),
        };
    }

    private enum BulkRobloxCopyMode
    {
        Asset,
        UgcBackport,
    }

    private sealed record BulkRobloxCopyCandidate(long RobloxAssetId, ProductDataResponse Details, int PriceRobux);

    private sealed class ExistingCopiedRobloxAssetRow
    {
        public long robloxAssetId { get; set; }
        public long assetId { get; set; }
    }

    private sealed class BulkContentDownloadResult
    {
        public ConcurrentDictionary<long, byte[]> contentByAssetId { get; } = new();
        public ConcurrentDictionary<long, Exception> errorsByAssetId { get; } = new();
    }

    private sealed record BulkBackportPrepared(
        BulkRobloxCopyCandidate Candidate,
        byte[] RbxmBytes,
        long MeshRobloxAssetId);

    private async Task CopyBulkRobloxAssetsAsync(
        IReadOnlyCollection<BulkRobloxCopyCandidate> candidates,
        BulkCopyAssetRequest request,
        AdminActorContext actor,
        IDictionary<long, BulkCopyAssetResult> resultByRobloxAssetId)
    {
        if (candidates.Count == 0)
            return;

        var assetDelivery = await TryGetAssetDeliveryV2BatchLookupAsync(candidates.Select(candidate => candidate.RobloxAssetId));
        var contentDownloads = await DownloadBulkRobloxAssetContentBytesAsync(candidates.Select(candidate => candidate.RobloxAssetId), assetDelivery);

        foreach (var candidate in candidates)
        {
            try
            {
                if (contentDownloads.errorsByAssetId.TryGetValue(candidate.RobloxAssetId, out var downloadError))
                    throw downloadError;

                if (!contentDownloads.contentByAssetId.TryGetValue(candidate.RobloxAssetId, out var contentBytes))
                    throw new Exception("Roblox asset content was not downloaded");

                await CopyItemFloodCheck(actor);
                await using var content = new MemoryStream(contentBytes);
                var assetDetails = await assets.CreateAsset(
                    candidate.Details.Name!,
                    candidate.Details.Description,
                    1,
                    CreatorType.User,
                    1,
                    content,
                    candidate.Details.AssetTypeId!.Value,
                    Genre.All,
                    ModerationStatus.ReviewApproved,
                    DateTime.UtcNow,
                    DateTime.UtcNow,
                    candidate.RobloxAssetId,
                    disableRender: true);

                await UpdateCreatedBulkCopyAssetAsync(assetDetails.assetId, candidate, request, actor);
                await assets.RenderAssetsInBurstAsync(
                    new[] { (assetDetails.assetId, candidate.Details.AssetTypeId.Value) });
                resultByRobloxAssetId[candidate.RobloxAssetId] = CreateBulkCopySuccess(candidate.RobloxAssetId, assetDetails.assetId, candidate.PriceRobux, false);
            }
            catch (Exception ex)
            {
                resultByRobloxAssetId[candidate.RobloxAssetId] = new BulkCopyAssetResult
                {
                    robloxAssetId = candidate.RobloxAssetId,
                    success = false,
                    error = GetBulkCopyErrorMessage(ex),
                };
            }
        }
    }

    private async Task BackportBulkRobloxAssetsAsync(
        IReadOnlyCollection<BulkRobloxCopyCandidate> candidates,
        BulkCopyAssetRequest request,
        AdminActorContext actor,
        IDictionary<long, BulkCopyAssetResult> resultByRobloxAssetId)
    {
        if (candidates.Count == 0)
            return;

        var primaryDelivery = await TryGetAssetDeliveryV2BatchLookupAsync(candidates.Select(candidate => candidate.RobloxAssetId));
        var primaryDownloads = await DownloadBulkRobloxAssetContentBytesAsync(candidates.Select(candidate => candidate.RobloxAssetId), primaryDelivery);
        var prepared = new List<BulkBackportPrepared>();
        var meshIds = new HashSet<long>();

        foreach (var candidate in candidates)
        {
            try
            {
                if (primaryDownloads.errorsByAssetId.TryGetValue(candidate.RobloxAssetId, out var downloadError))
                    throw downloadError;

                if (!primaryDownloads.contentByAssetId.TryGetValue(candidate.RobloxAssetId, out var rbxmBytes))
                    throw new Exception("Roblox UGC content was not downloaded");

                var meshId = ExtractBackportMeshId(rbxmBytes);
                meshIds.Add(meshId);
                prepared.Add(new BulkBackportPrepared(candidate, rbxmBytes, meshId));
            }
            catch (Exception ex)
            {
                resultByRobloxAssetId[candidate.RobloxAssetId] = new BulkCopyAssetResult
                {
                    robloxAssetId = candidate.RobloxAssetId,
                    success = false,
                    error = GetBulkCopyErrorMessage(ex),
                };
            }
        }

        var meshDetailsById = await TryGetCatalogItemDetailsBatchAsync(meshIds);
        var meshDelivery = await TryGetAssetDeliveryV2BatchLookupAsync(meshIds);
        var meshDownloads = await DownloadBulkRobloxAssetContentBytesAsync(meshIds, meshDelivery);

        foreach (var item in prepared)
        {
            var candidate = item.Candidate;
            try
            {
                var meshDetails = await GetBulkRobloxProductDetailsAsync(item.MeshRobloxAssetId, meshDetailsById);
                if (meshDetails.AssetTypeId != Type.Mesh)
                    throw new StaffException("The mesh request has failed");

                if (meshDownloads.errorsByAssetId.TryGetValue(item.MeshRobloxAssetId, out var meshDownloadError))
                    throw meshDownloadError;

                if (!meshDownloads.contentByAssetId.TryGetValue(item.MeshRobloxAssetId, out var meshBytes))
                    throw new Exception("Roblox mesh content was not downloaded");

                var newMeshBytes = AssetsService.ConvertMesh(meshBytes);
                await CopyItemFloodCheck(actor);
                await using var newMeshStream = new MemoryStream(newMeshBytes);
                var meshAsset = await assets.CreateAsset(
                    meshDetails.Name ?? candidate.Details.Name!,
                    meshDetails.Description,
                    1,
                    CreatorType.User,
                    1,
                    newMeshStream,
                    Type.Mesh,
                    Genre.All,
                    ModerationStatus.ReviewApproved,
                    DateTime.UtcNow,
                    DateTime.UtcNow,
                    item.MeshRobloxAssetId,
                    disableRender: true);

                var newRbxmBytes = RewriteBackportMeshId(item.RbxmBytes, item.MeshRobloxAssetId.ToString(), meshAsset.assetId.ToString());
                await using var newRbxmStream = new MemoryStream(newRbxmBytes);
                var assetDetails = await assets.CreateAsset(
                    candidate.Details.Name!,
                    candidate.Details.Description,
                    1,
                    CreatorType.User,
                    1,
                    newRbxmStream,
                    candidate.Details.AssetTypeId!.Value,
                    Genre.All,
                    ModerationStatus.ReviewApproved,
                    DateTime.UtcNow,
                    DateTime.UtcNow,
                    candidate.RobloxAssetId,
                    disableRender: true);

                await UpdateCreatedBulkCopyAssetAsync(assetDetails.assetId, candidate, request, actor);
                await assets.RenderAssetsInBurstAsync(new[]
                {
                    (meshAsset.assetId, Type.Mesh),
                    (assetDetails.assetId, candidate.Details.AssetTypeId.Value),
                });
                resultByRobloxAssetId[candidate.RobloxAssetId] = CreateBulkCopySuccess(candidate.RobloxAssetId, assetDetails.assetId, candidate.PriceRobux, false);
            }
            catch (Exception ex)
            {
                resultByRobloxAssetId[candidate.RobloxAssetId] = new BulkCopyAssetResult
                {
                    robloxAssetId = candidate.RobloxAssetId,
                    success = false,
                    error = GetBulkCopyErrorMessage(ex),
                };
            }
        }
    }

    private static IReadOnlyCollection<Type> GetBulkCopyAllowedTypes(BulkRobloxCopyMode mode)
    {
        return mode == BulkRobloxCopyMode.UgcBackport
            ? new List<Type>
            {
                Type.Hat, Type.HairAccessory, Type.FrontAccessory, Type.BackAccessory, Type.WaistAccessory,
                Type.NeckAccessory, Type.Gear, Type.ShoulderAccessory, Type.FaceAccessory,
                Type.Head, Type.EmoteAnimation, Type.Model
            }
            : new List<Type>
            {
                Type.Hat, Type.HairAccessory, Type.FrontAccessory, Type.BackAccessory, Type.WaistAccessory,
                Type.NeckAccessory, Type.Gear, Type.ShoulderAccessory, Type.FaceAccessory,
                Type.Head, Type.EmoteAnimation,
            };
    }

    private async Task<Dictionary<long, long>> GetExistingCopiedRobloxAssetIds(IEnumerable<long> robloxAssetIds)
    {
        var ids = robloxAssetIds.Distinct().ToArray();
        if (ids.Length == 0)
            return new Dictionary<long, long>();

        var rows = await db.QueryAsync<ExistingCopiedRobloxAssetRow>(
            "SELECT roblox_asset_id AS \"robloxAssetId\", id AS \"assetId\" FROM asset WHERE roblox_asset_id = ANY(@assetIds) ORDER BY id ASC",
            new { assetIds = ids });

        return rows
            .GroupBy(row => row.robloxAssetId)
            .ToDictionary(group => group.Key, group => group.First().assetId);
    }

    private async Task<Dictionary<long, ProductDataResponse>> TryGetCatalogItemDetailsBatchAsync(IEnumerable<long> robloxAssetIds)
    {
        var ids = robloxAssetIds.Distinct().ToList();
        if (ids.Count == 0)
            return new Dictionary<long, ProductDataResponse>();

        try
        {
            return await robloxApi.GetCatalogItemDetailsBatchAsync(ids);
        }
        catch (Exception ex)
        {
            Writer.Info(LogGroup.RealRobloxApi, "catalog items/details batch failed: {0}", ex.Message);
            return new Dictionary<long, ProductDataResponse>();
        }
    }

    private async Task<ProductDataResponse> GetBulkRobloxProductDetailsAsync(long robloxAssetId, IReadOnlyDictionary<long, ProductDataResponse> catalogDetails)
    {
        if (catalogDetails.TryGetValue(robloxAssetId, out var details))
            return details;

        return await robloxApi.GetProductInfo(robloxAssetId, true);
    }

    private async Task<Dictionary<long, AssetDeliveryV2BatchResponse>> TryGetAssetDeliveryV2BatchLookupAsync(IEnumerable<long> robloxAssetIds)
    {
        var ids = robloxAssetIds.Distinct().ToList();
        if (ids.Count == 0)
            return new Dictionary<long, AssetDeliveryV2BatchResponse>();

        try
        {
            var response = await RobloxApi.GetAssetDeliveryV2BatchAsync(ids.Select(assetId => new BatchAssetRequest
            {
                assetId = assetId,
                assetType = "Asset",
                requestId = assetId.ToString(),
            }).ToList());

            return response
                .Where(entry => !string.IsNullOrWhiteSpace(entry.requestId) && long.TryParse(entry.requestId, out _))
                .ToDictionary(entry => long.Parse(entry.requestId!), entry => entry);
        }
        catch (Exception ex)
        {
            Writer.Info(LogGroup.RealRobloxApi, "assetdelivery v2 batch failed: {0}", ex.Message);
            return new Dictionary<long, AssetDeliveryV2BatchResponse>();
        }
    }

    private async Task<BulkContentDownloadResult> DownloadBulkRobloxAssetContentBytesAsync(
        IEnumerable<long> robloxAssetIds,
        IReadOnlyDictionary<long, AssetDeliveryV2BatchResponse> batchEntries)
    {
        var result = new BulkContentDownloadResult();
        using var semaphore = new SemaphoreSlim(4);
        var tasks = robloxAssetIds.Distinct().Select(async assetId =>
        {
            await semaphore.WaitAsync();
            try
            {
                await using var stream = await DownloadRobloxAssetContentAsync(assetId, batchEntries);
                result.contentByAssetId[assetId] = EasyConverters.StreamToByte(stream);
            }
            catch (Exception ex)
            {
                result.errorsByAssetId[assetId] = ex;
            }
            finally
            {
                semaphore.Release();
            }
        });

        await Task.WhenAll(tasks);
        return result;
    }

    private async Task<Stream> DownloadRobloxAssetContentAsync(long robloxAssetId, IReadOnlyDictionary<long, AssetDeliveryV2BatchResponse> batchEntries)
    {
        if (batchEntries.TryGetValue(robloxAssetId, out var entry))
        {
            try
            {
                return await robloxApi.DownloadAssetContentFromBatchLocationAsync(entry);
            }
            catch (Exception ex)
            {
                Writer.Info(LogGroup.RealRobloxApi, "assetdelivery v2 location failed for {0}: {1}", robloxAssetId, ex.Message);
            }
        }

        return await robloxApi.GetAssetContentFromProxy(robloxAssetId);
    }

    private async Task UpdateCreatedBulkCopyAssetAsync(
        long assetId,
        BulkRobloxCopyCandidate candidate,
        BulkCopyAssetRequest request,
        AdminActorContext actor)
    {
        await InTransaction(async _ =>
        {
            using var txAssets = ServiceProvider.GetOrCreate<AssetsService>(this);
            await db.ExecuteAsync("INSERT INTO moderation_migrate_asset(asset_id, roblox_asset_id, actor_id) VALUES (@assetId, @robloxAssetId, @actorId)",
                new
                {
                    assetId = assetId,
                    robloxAssetId = candidate.RobloxAssetId,
                    actorId = actor.userId,
                });

            var details = await txAssets.GetProductForAsset(assetId);
            var existingLog = await db.QuerySingleOrDefaultAsync<Total>("SELECT count(*) as total FROM moderation_update_product WHERE asset_id = :asset_id", new
            {
                asset_id = assetId,
            });
            if (existingLog.total == 0)
                await InsertProductLog(assetId, 1, details.isLimited, details.isLimitedUnique, details.offsaleAt, details.serialCount, details.priceRobux, details.priceTickets, details.isForSale);

            var isForSale = !IsRobloxOffsale(candidate.Details) || !request.keepOffsaleProperty;
            var isLimited = request.keepLimitedProperties && candidate.Details.IsLimited == true;
            var isLimitedUnique = request.keepLimitedProperties && candidate.Details.IsLimitedUnique == true;

            await InsertProductLog(assetId, actor.userId, isLimited, isLimitedUnique, null, null, candidate.PriceRobux, null, isForSale);
            await txAssets.UpdateAssetMarketInfoName(assetId, candidate.Details.Name ?? string.Empty);
            await txAssets.UpdateAssetMarketDescriptionInfo(assetId, candidate.Details.Description ?? string.Empty);
            await txAssets.UpdateAssetMarketInfo(assetId, isForSale, isLimited, isLimitedUnique, null, null);
            await txAssets.SetItemPrice(assetId, candidate.PriceRobux, null);
            return 0;
        });
    }

    private static long ExtractBackportMeshId(byte[] rbxmBytes)
    {
        var rbxmHexString = Convert.ToHexString(rbxmBytes);
        var meshIdHexString = rbxmHexString
            .Split(EasyConverters.StringToHexString("MeshId"))[1]
            .Split(EasyConverters.StringToHexString("rbxassetid://"))[1]
            .Split(EasyConverters.StringToHexString("PROP"))[0];

        return long.Parse(EasyConverters.HexStringToString(meshIdHexString));
    }

    private static byte[] RewriteBackportMeshId(byte[] rbxmBytes, string oldMeshId, string newMeshId)
    {
        if (newMeshId.Length > oldMeshId.Length)
            throw new Exception("New MeshId too long");

        var newMeshIdHex = EasyConverters.StringToHexString(newMeshId);
        for (var i = 0; i < oldMeshId.Length - newMeshId.Length; i++)
        {
            newMeshIdHex = $"{newMeshIdHex}00";
        }

        var oldMeshIdHex = EasyConverters.StringToHexString(oldMeshId);
        return Convert.FromHexString(Convert.ToHexString(rbxmBytes).Replace(oldMeshIdHex, newMeshIdHex));
    }

    private async Task<long?> TryGetExistingCopiedRobloxAssetId(long robloxAssetId)
    {
        try
        {
            return await assets.GetAssetIdFromRobloxAssetId(robloxAssetId);
        }
        catch (RecordNotFoundException)
        {
            return null;
        }
    }

    private static string? GetBulkCopySkipReason(ProductDataResponse details, BulkCopyAssetRequest request)
    {
        var isOffsale = IsRobloxOffsale(details);
        if (IsRobloxLimited(details) && request.skipLimitedItems)
            return "Skipped limited item";
        if (isOffsale && request.skipOffsaleItems)
            return "Skipped offsale item";
        if (request.skipOpenedOffsaleGiftItems &&
            isOffsale &&
            details.Name?.Contains("Opened", StringComparison.OrdinalIgnoreCase) == true &&
            details.Name.Contains("Gift", StringComparison.OrdinalIgnoreCase))
            return "Skipped opened gift item";

        return null;
    }

    private static bool IsRobloxLimited(ProductDataResponse details)
    {
        return details.IsLimited == true || details.IsLimitedUnique == true;
    }

    private static bool IsRobloxOffsale(ProductDataResponse details)
    {
        return details.IsForSale == false;
    }

    private static int GetRobloxCopyPrice(ProductDataResponse details, BulkCopyAssetRequest request)
    {
        if (details.PriceInRobux is > 0)
            return details.PriceInRobux.Value;
        if (IsRobloxLimited(details) && request.limitedPriceRobux is > 0)
            return request.limitedPriceRobux.Value;

        return DefaultBulkRobloxAssetCopyPriceRobux;
    }

    private static int ApplyRobloxCopyDiscountPercent(int priceRobux, int? discountedPricePercent)
    {
        if (discountedPricePercent is null)
            return priceRobux;

        var clampedPercent = Math.Clamp(discountedPricePercent.Value, 0, 100);
        var remainingPercent = 100 - clampedPercent;
        return (int)Math.Round(priceRobux * (remainingPercent / 100m), MidpointRounding.AwayFromZero);
    }

    private static BulkCopyAssetResult CreateBulkCopySuccess(long robloxAssetId, long assetId, int? priceRobux, bool alreadyExisted)
    {
        return new BulkCopyAssetResult
        {
            robloxAssetId = robloxAssetId,
            assetId = assetId,
            catalogUrl = GetCatalogUrl(assetId),
            priceRobux = priceRobux,
            alreadyExisted = alreadyExisted,
            success = true,
        };
    }

    private static BulkCopyAssetResult CreateBulkCopySkipped(long robloxAssetId, string reason)
    {
        return new BulkCopyAssetResult
        {
            robloxAssetId = robloxAssetId,
            success = false,
            error = reason,
        };
    }

    private static string GetCatalogUrl(long assetId)
    {
        var path = $"/catalog/{assetId}/--";
        return string.IsNullOrWhiteSpace(Roblox.Configuration.ShortBaseUrl)
            ? path
            : $"https://www.{Roblox.Configuration.ShortBaseUrl}{path}";
    }

    private static string GetBulkCopyErrorMessage(Exception ex)
    {
        return ex is RobloxException robloxException && !string.IsNullOrWhiteSpace(robloxException.errorMessage)
            ? robloxException.errorMessage
            : ex.Message;
    }

    public async Task<AdminAssetIdResponse> BackportAssetFromRobloxAsync(CopyAssetRequest request, AdminActorContext actor, bool allowLimitedCopyWithoutLimitedPermission = false)
    {
        if (!request.force)
        {
            try
            {
                return new AdminAssetIdResponse { assetId = await assets.GetAssetIdFromRobloxAssetId(request.assetId) };
            }
            catch (RecordNotFoundException)
            {
            }
        }

        var details = await robloxApi.GetProductInfo(request.assetId, true);
        var allowedTypes = new List<Type>
        {
            Type.Hat, Type.HairAccessory, Type.FrontAccessory, Type.BackAccessory, Type.WaistAccessory,
            Type.NeckAccessory, Type.Gear, Type.ShoulderAccessory, Type.FaceAccessory,
            Type.Head, Type.EmoteAnimation, Type.Model
        };
        ValidateRobloxCopyDetails(details, allowedTypes, actor, allowLimitedCopyWithoutLimitedPermission);

        if (!request.force)
            await EnsureNoRobloxCopyDuplicate(details);

        await CopyItemFloodCheck(actor);
        var backportId = await assets.BackportAccessory(request.assetId, renderInBurst: true);
        if (backportId == 0)
            throw new StaffException("Failed to backport asset");
        await db.ExecuteAsync("INSERT INTO moderation_migrate_asset(asset_id, roblox_asset_id, actor_id) VALUES (@assetId, @robloxAssetId, @actorId)",
            new
            {
                assetId = backportId,
                robloxAssetId = request.assetId,
                actorId = actor.userId,
            });

        return new AdminAssetIdResponse { assetId = backportId };
    }

    public async Task<AdminAssetIdResponse> CopyAssetFromRobloxAsync(CopyAssetRequest request, AdminActorContext actor, bool allowLimitedCopyWithoutLimitedPermission = false)
    {
        if (!request.force)
        {
            try
            {
                return new AdminAssetIdResponse { assetId = await assets.GetAssetIdFromRobloxAssetId(request.assetId) };
            }
            catch (RecordNotFoundException)
            {
            }
        }

        var details = await robloxApi.GetProductInfo(request.assetId, true);
        var allowedTypes = new List<Type>
        {
            Type.Hat, Type.HairAccessory, Type.FrontAccessory, Type.BackAccessory, Type.WaistAccessory,
            Type.NeckAccessory, Type.Gear, Type.ShoulderAccessory, Type.FaceAccessory,
            Type.Head, Type.EmoteAnimation,
        };
        ValidateRobloxCopyDetails(details, allowedTypes, actor, allowLimitedCopyWithoutLimitedPermission);

        if (!request.force)
            await EnsureNoRobloxCopyDuplicate(details);

        var content = await robloxApi.GetAssetContentFromProxy(request.assetId);
        content.Position = 0;
        await CopyItemFloodCheck(actor);
        var assetDetails = await assets.CreateAsset(details.Name, details.Description, 1,
            CreatorType.User, 1, content, details.AssetTypeId!.Value, Genre.All, ModerationStatus.ReviewApproved,
            DateTime.UtcNow, DateTime.UtcNow, request.assetId, renderInBurst: true);
        await db.ExecuteAsync("INSERT INTO moderation_migrate_asset(asset_id, roblox_asset_id, actor_id) VALUES (@assetId, @robloxAssetId, @actorId)",
            new
            {
                assetId = assetDetails.assetId,
                robloxAssetId = request.assetId,
                actorId = actor.userId,
            });

        return new AdminAssetIdResponse { assetId = assetDetails.assetId };
    }

    private void ValidateRobloxCopyDetails(ProductDataResponse details, IReadOnlyCollection<Type> allowedTypes, AdminActorContext actor, bool allowLimitedCopyWithoutLimitedPermission = false)
    {
        if (details.AssetTypeId == null || !allowedTypes.Contains(details.AssetTypeId.Value))
            throw new StaffException("Cannot copy this assetType: " + details.AssetTypeId);
        if (string.IsNullOrWhiteSpace(details.Name))
            throw new StaffException("Name cannot be null or empty");
        if (details.IsLimited == null || details.IsLimitedUnique == null)
            throw new StaffException("Product details were invalid for this item. Try again");

        if ((details.IsLimited == true || details.IsLimitedUnique == true) &&
            !allowLimitedCopyWithoutLimitedPermission &&
            !HasPermission(actor, Access.MakeItemLimited))
            throw new StaffException("You do not have permission to copy a limited item");
    }

    private async Task EnsureNoRobloxCopyDuplicate(ProductDataResponse details)
    {
        var alreadyExists = await assets.SearchCatalog(new CatalogSearchRequest
        {
            limit = 10,
            include18Plus = true,
            includeNotForSale = true,
            creatorType = CreatorType.User,
            creatorTargetId = 1,
            keyword = details.Name,
        });
        if (alreadyExists._total == 0 || alreadyExists.data == null)
            return;

        foreach (var item in alreadyExists.data)
        {
            var info = await assets.GetAssetCatalogInfo(item.id);
            if (info.assetType == details.AssetTypeId)
                throw new StaffException("It looks like this item already exists: AssetID=" + info.id +
                                         "\nIf this is incorrect, click the 'force' button to upload this item anyway.");
        }
    }

    public async Task ResetDescriptionAsync(long userId)
    {
        var rlKey = "ResetDescriptionV1";
        if ((await redis.StringGetAsync(rlKey)) != null)
            throw new StaffException("Someone already reset a description recently. Try again in a few seconds.");

        await redis.StringSetAsync(rlKey, "{}", TimeSpan.FromSeconds(5));
        await users.SetUserDescription(userId, "[ Content Deleted ]");
    }

    public async Task ResetUsernameAsync(long userId, AdminActorContext actor, Func<long, bool> isOwnerUserId)
    {
        if (!actor.isOwner)
        {
            var rlKey = "ResetUsernameV1";
            if ((await redis.StringGetAsync(rlKey)) != null)
                throw new StaffException("Someone already reset a username recently. Try again in a few seconds.");
            await redis.StringSetAsync(rlKey, "{}", TimeSpan.FromSeconds(5));
        }

        var userData = await users.GetUserById(userId);
        if (userData.isModerator || userData.isAdmin || await IsStaffAsync(userData.userId, isOwnerUserId))
            throw new StaffException("Cannot change this user's username");
        await users.AddBadUsername(userData.username);
        await users.ResetUsername(userId, actor.userId);
        await privateMessages.CreateMessage(userId, 1, "Username Reset",
            "Hello,\n\nYour username has been reset due to abuse concerns. You can request a new username by contacting a staff member.\n\n-The Averia Team");
    }

    public async Task VerifyUserAsync(long userId, AdminActorContext actor)
    {
        RequireOwner(actor, "You are not allowed to access this");
        await db.ExecuteAsync("UPDATE \"user\" SET verified = true WHERE id = :uid", new { uid = userId });
        await users.InvalidateUserInfoCache(userId);
    }

    public async Task UnverifyUserAsync(long userId, AdminActorContext actor)
    {
        RequireOwner(actor, "You are not allowed to access this");
        await db.ExecuteAsync("UPDATE \"user\" SET verified = false WHERE id = :uid", new { uid = userId });
        await users.InvalidateUserInfoCache(userId);
    }

    public Task UpdateLocksAsync(string ids, AdminActorContext actor)
    {
        var parsed = ids.Split(",");
        return parsed.Length is < 0 or > 10 ? Task.CompletedTask : users.AcquireApplicationLocks(actor.userId, parsed);
    }

    public Task<IEnumerable<UserApplicationEntry>> GetApplicationsAsync(UserApplicationStatus? status, int offset, SortOrder sortOrder, string? searchQuery, ApplicationSearchColumn? searchColumn, AdminActorContext actor)
    {
        return users.GetApplications(status, offset, sortOrder, status == UserApplicationStatus.Pending ? actor.userId : null, searchQuery, searchColumn);
    }

    public async Task<UserApplicationEntry> GetApplicationByIdAsync(string id)
    {
        var result = await users.GetApplicationById(id);
        if (result == null)
            throw new StaffException("Application ID is invalid or does not exist");
        return result;
    }

    public async Task<AdminCountResponse> GetNumPendingApplicationsAsync()
    {
        return new AdminCountResponse { count = await users.CountPendingApplications() };
    }

    public async Task<AdminApplicationApproveResponse> ApproveApplicationAsync(string applicationId, AdminActorContext actor)
    {
        var appInfo = await users.GetApplicationById(applicationId);
        if (appInfo?.status == UserApplicationStatus.Pending)
            await AwardCommissionForApplicationReviewAsync(actor.userId);
        else if (appInfo?.status == UserApplicationStatus.Approved || appInfo?.status == UserApplicationStatus.Rejected)
            throw new StaffException("Application is already approved or rejected");
        var result = await users.ProcessApplication(applicationId, actor.userId, UserApplicationStatus.Approved);
        return new AdminApplicationApproveResponse { joinId = result };
    }

    public async Task DeclineApplicationAsync(string applicationId, string reason, AdminActorContext actor)
    {
        var appInfo = await users.GetApplicationById(applicationId);
        if (appInfo?.status == UserApplicationStatus.Pending)
            await AwardCommissionForApplicationReviewAsync(actor.userId);
        await users.ProcessApplication(applicationId, actor.userId, UserApplicationStatus.Rejected, reason);
    }

    public async Task DeclineApplicationSilentlyAsync(string applicationId, AdminActorContext actor)
    {
        var appInfo = await users.GetApplicationById(applicationId);
        if (appInfo?.status == UserApplicationStatus.Pending)
            await AwardCommissionForApplicationReviewAsync(actor.userId);
        await users.ProcessApplication(applicationId, actor.userId, UserApplicationStatus.SilentlyRejected);
    }

    public Task ClearApplicationAsync(string applicationId)
    {
        return users.ClearApplication(applicationId);
    }

    public Task<IEnumerable<UserInviteEntry>> GetInvitesByUserAsync(long userId)
    {
        return users.GetInvitesByUser(userId);
    }

    public async Task<AdminLatestTextModerationIdsResponse> GetLatestIdsForTextModAsync()
    {
        var forumPosts = await forums.GetAllPosts(0, 1, "desc", null);
        var comments = await GetAllAssetCommentsAsync(1, 0, "desc");
        var wall = await GetAllWallPostsAsync(1, 0, "desc");
        var status = await GetAllUserStatusesAsync(0, 1, "desc");
        var groupStatus = await GetGroupStatusesAsync(0, 1, "desc");

        return new AdminLatestTextModerationIdsResponse
        {
            ForumPost = forumPosts.Last().postId,
            AssetComment = comments.Last().id,
            GroupWallPost = wall.Last().id,
            UserStatusPost = status.Last().id,
            GroupStatusPost = groupStatus.Last().id,
        };
    }

    public async Task<IEnumerable<StaffAssetCommentEntry>> GetAllAssetCommentsAsync(int limit, int offset, string? sortOrder = "asc", long? exclusiveStartId = 0)
    {
        var q = new SqlBuilder();
        var t = q.AddTemplate(
            "SELECT asset_comment.id as id, asset.id as assetId, asset.name, asset_comment.comment as comment, u.id as userId, u.username as username, asset_comment.created_at as createdAt FROM asset_comment INNER JOIN asset ON asset_comment.asset_id = asset.id INNER JOIN \"user\" u ON asset_comment.user_id = u.id /**where**/ /**orderby**/ LIMIT :limit OFFSET :offset", new { limit, offset });
        if (exclusiveStartId != null)
            q.Where("asset_comment.id > :start_id", new { start_id = exclusiveStartId.Value });
        q.OrderBy(sortOrder == "desc" ? "asset_comment.id DESC" : "asset_comment.id ASC");
        return await db.QueryAsync<StaffAssetCommentEntry>(t.RawSql, t.Parameters);
    }

    public async Task<IEnumerable<StaffWallEntry>> GetAllWallPostsAsync(int limit, int offset, string? sortOrder = "asc", long? exclusiveStartId = null)
    {
        var q = new SqlBuilder();
        var t = q.AddTemplate(
            "SELECT gw.id, gw.content as post, gw.group_id as groupId, gw.user_id as userId, u.username, gw.created_at as createdAt FROM group_wall gw INNER JOIN \"user\" u ON gw.user_id = u.id /**where**/ /**orderby**/ LIMIT :limit OFFSET :offset",
            new { limit, offset });
        if (exclusiveStartId != null)
            q.Where("gw.id > :start_id", new { start_id = exclusiveStartId.Value });
        q.OrderBy(sortOrder == "desc" ? "gw.id desc" : "gw.id asc");
        return await db.QueryAsync<StaffWallEntry>(t.RawSql, t.Parameters);
    }

    public Task RemoveWallPostAsync(long id)
    {
        return db.ExecuteAsync("UPDATE group_wall SET \"content\" = '[ Content Deleted ]' WHERE id = :id", new { id });
    }

    public async Task<IEnumerable<GroupWallPostStaff>> GetGroupStatusesAsync(int offset, int limit, string? sortOrder = "asc", long? exclusiveStartId = null)
    {
        var q = new SqlBuilder();
        var t = q.AddTemplate(
            "SELECT s.id, s.group_id, s.status, s.user_id, g.name, u.username, s.created_at FROM group_status s INNER JOIN \"group\" g ON s.group_id = g.id INNER JOIN \"user\" u ON g.user_id = u.id /**where**/ /**orderby**/ LIMIT :limit OFFSET :offset",
            new { limit, offset });
        q.OrderBy(sortOrder == "desc" ? "s.id DESC" : "s.id ASC");
        if (exclusiveStartId != null)
            q.Where("s.id > :start_id", new { start_id = exclusiveStartId.Value });
        return await db.QueryAsync<GroupWallPostStaff>(t.RawSql, t.Parameters);
    }

    public Task DeleteGroupStatusAsync(long id)
    {
        return groups.DeleteGroupStatus(id);
    }

    public async Task<IEnumerable<StaffUserStatusEntry>> GetAllUserStatusesAsync(int offset, int limit, string? sortOrder = "asc", long? exclusiveStartId = null)
    {
        var q = new SqlBuilder();
        var t = q.AddTemplate(
            "SELECT s.id as id, s.user_id as userId, s.status as post, u.username, s.created_at as createdAt FROM user_status s INNER JOIN \"user\" u ON s.user_id = u.id /**where**/ /**orderby**/ LIMIT :limit OFFSET :offset",
            new { limit, offset });
        if (exclusiveStartId != null)
            q.Where("s.id > :start_id", new { start_id = exclusiveStartId.Value });
        q.OrderBy(sortOrder == "desc" ? "s.id DESC" : "s.id ASC");
        return await db.QueryAsync<StaffUserStatusEntry>(t.RawSql, t.Parameters);
    }

    public async Task<IReadOnlyCollection<AdminDataRow>> GetGroupListAsync(int offset, int limit, string sortColumn, string sortOrder)
    {
        if (!allowedGroupSortColumns.Contains(sortColumn))
            sortColumn = allowedGroupSortColumns[0];
        if (sortOrder is not "asc" or "desc")
            sortOrder = "asc";
        var sql = new SqlBuilder();
        var t = sql.AddTemplate("SELECT * FROM \"group\" g /**orderby**/");
        sql.OrderBy($"{sortColumn} {sortOrder} LIMIT :limit OFFSET :offset", new { limit, offset });
        var rows = await db.QueryAsync(t.RawSql, t.Parameters);
        return ToAdminRows(rows);
    }

    public async Task<AdminGroupModerationInfoResponse> GetGroupByNameAsync(string name)
    {
        var id = await groups.GetGroupIdByName(name);
        return await GetGroupModerationInfoAsync(id);
    }

    public async Task<IReadOnlyCollection<AdminDataRow>> GetEntireAuditLogAsync(long groupId)
    {
        var rows = await db.QueryAsync(
            "SELECT * FROM group_audit_log WHERE group_id = :gid ORDER BY group_audit_log.id DESC", new { gid = groupId });
        return ToAdminRows(rows);
    }

    public Task ToggleGroupLockStatusAsync(long groupId, bool locked)
    {
        return db.ExecuteAsync("UPDATE \"group\" g SET locked = :t WHERE g.id = :id", new { id = groupId, t = locked });
    }

    public async Task ResetGroupAsync(long groupId)
    {
        var newName = "[ Content Deleted (" + groupId + ") ]";
        if (await groups.IsGroupNameTaken(newName))
            newName = Guid.NewGuid().ToString();
        await db.ExecuteAsync(
            "UPDATE \"group\" SET name = :name, description = '[ Content Deleted ]' WHERE id = :id",
            new { id = groupId, name = newName });
        foreach (var entry in await groups.MultiGetGroupStatus(new[] { groupId }, 100000))
            await db.ExecuteAsync("UPDATE group_status SET status = '[ Content Deleted ]' WHERE id = :id", new { id = entry.feedId });
        foreach (var item in await groups.GetRolesInGroup(groupId))
        {
            if (item.rank == 0)
                continue;
            var name = "Role" + item.id;
            await db.ExecuteAsync(
                "UPDATE group_role SET name = :name, description = '[ Content Deleted ]' WHERE id =:id", new { item.id, name });
        }
        await db.ExecuteAsync("UPDATE group_icon SET is_approved = 0 WHERE group_id = :id", new { id = groupId });
        await db.ExecuteAsync(
            "UPDATE group_audit_log SET new_description = '[ Content Deleted ]', old_description = '[ Content Deleted ]' WHERE new_description IS NOT NULL AND group_id = :id",
            new { id = groupId });
        await db.ExecuteAsync(
            "UPDATE group_audit_log SET new_name = '[ Content Deleted ]', old_name = '[ Content Deleted ]' WHERE new_name IS NOT NULL AND group_id = :id",
            new { id = groupId });
        await db.ExecuteAsync(
            "UPDATE group_audit_log SET post_desc = '[ Content Deleted ]' WHERE post_desc IS NOT NULL AND group_id = :id",
            new { id = groupId });
        await db.ExecuteAsync("UPDATE group_wall SET content = '[ Content Deleted ]' WHERE group_id = :id", new { id = groupId });
    }

    public async Task<IReadOnlyCollection<AdminDataRow>> GetPlayHistoryAsync(int limit, int offset)
    {
        var rows = await db.QueryAsync(
            "SELECT p.asset_id, p.user_id, p.created_at, p.ended_at, a.name, u.username FROM asset_play_history p INNER JOIN asset a ON p.asset_id = a.id INNER JOIN \"user\" u ON p.user_id = u.id ORDER BY p.id DESC LIMIT :limit OFFSET :offset",
            new { limit, offset });
        return ToAdminRows(rows);
    }

    public async Task<AdminRobuxAmountResponse> RequestPaymentAsync(AdminActorContext actor)
    {
        var redisKey = "TextModerator:Clock:v2:" + actor.userId;
        var lastTimeStr = await redis.StringGetAsync(redisKey);
        var lastClock = DateTime.UtcNow;
        if (lastTimeStr != null)
        {
            var result = JsonSerializer.Deserialize<DateTimeSerialized>(lastTimeStr);
            if (result != null)
                lastClock = result.clock;
        }
        await redis.StringSetAsync(redisKey, JsonSerializer.Serialize(new DateTimeSerialized { clock = DateTime.UtcNow }));

        var forumPosts = await forums.GetAllPosts(0, 100, "desc", null);
        var comments = await GetAllAssetCommentsAsync(100, 0, "desc");
        var wall = await GetAllWallPostsAsync(100, 0, "desc");
        var status = await GetAllUserStatusesAsync(0, 100, "desc");
        var groupStatus = await GetGroupStatusesAsync(0, 100, "desc");

        const int robuxMultiplier = 5;
        var robuxAmount = comments.Count(c => c.createdAt > lastClock) +
                          forumPosts.Count(c => c.createdAt > lastClock) +
                          wall.Count(c => c.createdAt > lastClock) +
                          status.Count(c => c.createdAt > lastClock) +
                          groupStatus.Count(c => c.created_at > lastClock);
        if (robuxAmount == 0)
            return new AdminRobuxAmountResponse { robuxAmount = robuxAmount };

        robuxAmount *= robuxMultiplier;
        if (robuxAmount > 150)
            robuxAmount = 150;

        await economy.IncrementCurrency(CreatorType.User, actor.userId, CurrencyType.Robux, robuxAmount);
        await users.InsertAsync("user_transaction", new
        {
            type = PurchaseType.Commission,
            currency_type = CurrencyType.Robux,
            amount = robuxAmount,
            sub_type = TransactionSubType.StaffTextModeration,
            user_id_one = actor.userId,
            user_id_two = 1,
        });

        return new AdminRobuxAmountResponse { robuxAmount = robuxAmount };
    }

    public async Task<AdminChatMessagesResponse> GetChatMessagesAsync(string reportId)
    {
        var gameMessages = await abuseReport.GetGamesMessagesById(reportId);
        return new AdminChatMessagesResponse
        {
            content = $"These messages were recorded at: {gameMessages.createdAt:yyyy-MM-dd HH:mm:ss} in the game job {gameMessages.jobId}.\n\n" + gameMessages.messages,
        };
    }

    public async Task<AdminCountResponse> GetPendingReportsAsync()
    {
        return new AdminCountResponse { count = await abuseReport.CountPendingReports() };
    }

    public Task<IEnumerable<AbuseReportEntry>> GetReportsAsync(AbuseReportStatus status)
    {
        return abuseReport.GetReports(status);
    }

    public async Task AcceptReportAsync(string id, AdminActorContext actor)
    {
        var data = await abuseReport.GetReportById(id);
        if (data == null || data.reportStatus != AbuseReportStatus.Pending)
            return;
        await abuseReport.SetReportStatus(id, AbuseReportStatus.Valid, actor.userId);
        await privateMessages.CreateMessage(data.userId, 1, "Thank you for your report", "Your report has been reviewed and accepted. Thank you for helping keep Averia safe.");
        await RewardForReportReviewAsync(actor.userId);
    }

    public async Task DeclineReportAsync(string id, AdminActorContext actor)
    {
        var data = await abuseReport.GetReportById(id);
        if (data == null || data.reportStatus != AbuseReportStatus.Pending)
            return;
        await abuseReport.SetReportStatus(id, AbuseReportStatus.InvalidGood, actor.userId);
        await RewardForReportReviewAsync(actor.userId);
    }

    public async Task DeclineReportInvalidAsync(string id, AdminActorContext actor)
    {
        var data = await abuseReport.GetReportById(id);
        if (data == null || data.reportStatus != AbuseReportStatus.Pending)
            return;
        await abuseReport.SetReportStatus(id, AbuseReportStatus.InvalidBad, actor.userId);
        await RewardForReportReviewAsync(actor.userId);
    }

    public async Task<IEnumerable<CollectibleUserAssetEntry>> GetAllOwnersAsync(long assetId)
    {
        return await db.QueryAsync<CollectibleUserAssetEntry>("SELECT id as userAssetId, asset_id as assetId, user_id as userId, price, serial, created_at as createdAt, updated_at as updatedAt FROM user_asset WHERE asset_id = :asset_id", new { asset_id = assetId });
    }

    public async Task<StaffAssetResolveThumbnailResponse> GetDetailsFromThumbnailAsync(string url)
    {
        if (string.IsNullOrWhiteSpace(url))
            return new StaffAssetResolveThumbnailResponse();

        var response = new StaffAssetResolveThumbnailResponse();
        var assetUrl = matchAssetThumbRegex.Match(url);
        if (assetUrl.Success)
        {
            var groupData = assetUrl.Groups[1].Value;
            var matchingThumbs = await db.QueryAsync<ResolveThumbAssetEntry>("SELECT asset_id as assetId FROM asset_thumbnail WHERE content_url = :url", new { url = groupData });
            response.assets = matchingThumbs;
            var matchingVersions = await db.QueryAsync<ResolveThumbAssetEntry>("SELECT asset_id as assetId FROM asset_version WHERE content_url = :url", new { url = groupData });
            var list = response.assets.ToList();
            list.AddRange(matchingVersions);
            response.assets = list;
        }

        var thumbOrHeadshotUrl = matchUserThumbRegex.Match(url);
        if (thumbOrHeadshotUrl.Success)
            response.users = await db.QueryAsync<ResolveThumbUsersEntry>("SELECT user_id as userId FROM user_avatar WHERE thumbnail_url = :url OR headshot_thumbnail_url = :url", new { url = thumbOrHeadshotUrl.Groups[1].Value });

        var groupUrl = matchGroupIconRegex.Match(url);
        if (groupUrl.Success)
            response.groups = await db.QueryAsync<ResolveThumbGroupsEntry>("SELECT group_id as groupId FROM group_icon WHERE name = :url", new { url = groupUrl.Groups[1].Value });

        return response;
    }

    public Task<long> GetPerfTotalsAssetAsync(long userId)
    {
        return db.QuerySingleOrDefaultAsync<long>(@"
                WITH ordered_mma AS (
                    SELECT 
                        mma.created_at,
                        LAG(mma.created_at) OVER (PARTITION BY actor_id ORDER BY mma.created_at) AS prev_created_at 
                    FROM moderation_manage_asset as mma
                        INNER JOIN asset ON asset.id = mma.asset_id
                    WHERE actor_id = :userId AND asset.asset_type != :audioType
                )
                SELECT 
                    COUNT(*) 
                FROM ordered_mma 
                    WHERE prev_created_at IS NULL
                        OR EXTRACT(EPOCH FROM(created_at - prev_created_at)) * 1000 >= 500
                ", new { userId, audioType = Type.Audio });
    }

    public Task<long> GetPerfTotalsAudiosAsync(long userId)
    {
        return db.QuerySingleOrDefaultAsync<long>("SELECT COUNT(*) FROM moderation_manage_asset as mma INNER JOIN asset ON asset.id = mma.asset_id WHERE actor_id = :userId AND asset.asset_type = :audioType", new { userId, audioType = Type.Audio });
    }

    public Task<long> GetPerfTotalsApplicationsAsync(long userId)
    {
        return db.QuerySingleOrDefaultAsync<long>("SELECT COUNT(*) FROM moderation_change_join_app WHERE author_user_id = :userId", new { userId });
    }

    public Task<long> GetPerfTotalsReportsAsync(long userId)
    {
        return db.QuerySingleOrDefaultAsync<long>("SELECT COUNT(*) FROM abuse_report WHERE author_id = :userId AND report_status != :pending", new { userId, pending = AbuseReportStatus.Pending });
    }

    public Task<long> GetPerfTotalsPlayersModeratedAsync(long userId)
    {
        return db.QuerySingleOrDefaultAsync<long>("SELECT COUNT(*) FROM moderation_ban WHERE actor_id = :userId", new { userId });
    }

    public async Task<AdminDateResponse> GetPerfPermDateAsync(long userId)
    {
        return new AdminDateResponse
        {
            date = await db.QuerySingleOrDefaultAsync<DateTime?>("SELECT MIN(created_at) FROM user_permission WHERE user_id = :userId", new { userId }),
        };
    }

    private async Task AwardCommissionForModerationAsync(long actorUserId)
    {
        await economy.IncrementCurrency(CreatorType.User, actorUserId, CurrencyType.Robux, 5);
        await users.InsertAsync("user_transaction", new
        {
            type = PurchaseType.Commission,
            currency_type = CurrencyType.Robux,
            amount = 5,
            sub_type = TransactionSubType.StaffAssetModeration,
            user_id_one = actorUserId,
            user_id_two = 1,
        });
    }

    private async Task AwardCommissionForApplicationReviewAsync(long actorUserId)
    {
        await economy.IncrementCurrency(CreatorType.User, actorUserId, CurrencyType.Robux, 5);
        await users.InsertAsync("user_transaction", new
        {
            type = PurchaseType.Commission,
            currency_type = CurrencyType.Robux,
            amount = 5,
            sub_type = TransactionSubType.StaffApplicationReview,
            user_id_one = actorUserId,
            user_id_two = 1,
        });
    }

    private async Task RewardForReportReviewAsync(long actorUserId)
    {
        const int robuxAmount = 5;
        await economy.IncrementCurrency(CreatorType.User, actorUserId, CurrencyType.Robux, robuxAmount);
        await users.InsertAsync("user_transaction", new
        {
            type = PurchaseType.Commission,
            currency_type = CurrencyType.Robux,
            amount = robuxAmount,
            sub_type = TransactionSubType.StaffReportReview,
            user_id_one = actorUserId,
            user_id_two = 1,
        });
    }

    private static string NormalizeGroupIconName(string? iconName)
    {
        if (string.IsNullOrWhiteSpace(iconName))
            throw new StaffException("Invalid icon");

        if (iconName.IndexOf("/", StringComparison.Ordinal) != -1)
        {
            var location = iconName.LastIndexOf("/", StringComparison.Ordinal) + 1;
            iconName = iconName[location..];
        }

        if (iconName.IndexOf("/", StringComparison.Ordinal) != -1 || iconName.IndexOf("\\", StringComparison.Ordinal) != -1)
            throw new StaffException("Invalid filename: " + iconName);

        return iconName;
    }

    private static string FormatMacAddress(string rawMac)
    {
        return BitConverter.ToString(PhysicalAddress.Parse(rawMac.ToUpper().Replace(":", "")).GetAddressBytes()).Replace("-", ":");
    }

    private static string NormalizeIpHash(string ipHash)
    {
        var normalized = ipHash?.Trim() ?? string.Empty;
        if (normalized.Length is < 1 or > 128)
            throw new StaffException("IP hash must contain between 1 and 128 characters");
        return normalized;
    }

    private static bool DoesActionHaveActioned(string action)
    {
        return action is "ban" or "unban" or "item" or "message";
    }

    private async Task<AssetVersionEntry?> TryGetLatestAssetVersionAsync(long assetId)
    {
        try
        {
            return await assets.GetLatestAssetVersion(assetId);
        }
        catch (RecordNotFoundException)
        {
            return null;
        }
    }

    private async Task<(long creatorId, string creatorName)> ResolveCreatorAsync(AssetMultiGetEntry assetInfo, AssetVersionEntry? latestVersion)
    {
        if (latestVersion != null)
        {
            try
            {
                var userInfo = await users.GetUserById(latestVersion.creatorId);
                return (latestVersion.creatorId, userInfo.username);
            }
            catch (RecordNotFoundException)
            {
            }
        }

        var creatorId = assetInfo.creatorType == CreatorType.User ? assetInfo.creatorTargetId : 1;
        var creatorName = string.IsNullOrWhiteSpace(assetInfo.creatorName) ? "ROBLOX" : assetInfo.creatorName;
        return (creatorId, creatorName);
    }

    private sealed class AssetIdRow
    {
        public long assetId { get; set; }
    }

    private sealed class AssetIconModerationRow
    {
        public ModerationStatus moderation_status { get; set; }
        public string? content_url { get; set; }
        public long asset_id { get; set; }
    }

    private sealed class MacAccountCountRow
    {
        public string macAddress { get; set; } = string.Empty;
        public long userCount { get; set; }
    }

    private sealed class AltAccountUserRow
    {
        public long id { get; set; }
        public string username { get; set; } = string.Empty;
        public AccountStatus status { get; set; }
    }

    private sealed class MacIdentitySearchRow
    {
        public long id { get; set; }
        public string username { get; set; } = string.Empty;
        public AccountStatus status { get; set; }
        public string[] macAddresses { get; set; } = Array.Empty<string>();
    }

    private sealed class IpIdentitySearchRow
    {
        public long id { get; set; }
        public string username { get; set; } = string.Empty;
        public AccountStatus status { get; set; }
        public string[] actions { get; set; } = Array.Empty<string>();
        public DateTimeOffset createdAt { get; set; }
        public DateTimeOffset updatedAt { get; set; }
    }

    private sealed class AltAccountEvidenceRow
    {
        public long id { get; set; }
        public string username { get; set; } = string.Empty;
        public AccountStatus status { get; set; }
        public string[] macAddresses { get; set; } = Array.Empty<string>();
    }

    private sealed record AltAccountEvidence(
        AltAccountEvidenceRow row,
        int candidateMacCount,
        int sharedMacCount,
        int unionCount,
        bool exactMacSet);

    private sealed class AltSharedIpRow
    {
        public long userId { get; set; }
        public int sharedIpHashCount { get; set; }
    }

    private sealed class ModerationBanHistoryRow
    {
        public long id { get; set; }
        public long user_id { get; set; }
        public string? reason { get; set; }
        public string? internal_reason { get; set; }
        public DateTime created_at { get; set; }
        public DateTime? expired_at { get; set; }
        public long actor_id { get; set; }
    }

    private sealed class StaffException : RobloxException
    {
        public StaffException(string errorMessage = "") : base(500, 0, errorMessage)
        {
        }
    }

    private sealed class UnauthorizedException : RobloxException
    {
        public UnauthorizedException(string errorMessage = "Authorization has been denied for this request.") : base(401, 0, errorMessage)
        {
        }
    }

    private sealed class TooManyRequestsException : RobloxException
    {
        public TooManyRequestsException(string errorMessage = "TooManyRequests") : base(RobloxException.TooManyRequests, 0, errorMessage)
        {
        }
    }

    private sealed class BadRequestException : RobloxException
    {
        public BadRequestException(int errorCode = 0, string errorMessage = "") : base(RobloxException.BadRequest, errorCode, errorMessage)
        {
        }
    }

    private sealed class NotFoundException : RobloxException
    {
        public NotFoundException(int errorCode = 0, string errorMessage = "") : base(RobloxException.NotFound, errorCode, errorMessage)
        {
        }
    }
}
