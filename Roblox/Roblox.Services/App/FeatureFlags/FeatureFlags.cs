using System.Text.Json;
using Roblox.Services.Exceptions;

namespace Roblox.Services.App.FeatureFlags;

public enum FeatureFlag
{
    // All group related features
    GroupsEnabled = 1,
    // Specifically trading (both reading and writing)
    TradingEnabled,
    // Economy, including purchasing, viewing product info, viewing inventories, trading, etc
    EconomyEnabled,
    // Asset comment viewing and posting
    AssetCommentsEnabled,
    // User feed system, read and write
    UserFeedEnabled,
    // Games, Joining Games, Uploading, Viewing, etc
    GamesEnabled,
    // Game joining specifically. If disabled, all tickets will be marked as invalid and new tickets will not be generated
    GameJoinEnabled,
    // Private messages, read/write
    PrivateMessagesEnabled,
    // Avatar, write only
    AvatarsEnabled,
    LoginEnabled,
    SignupEnabled,
    ChangeUsernameEnabled,
    ChangePasswordEnabled,
    // Affects both uploads and the entire advertising system itself (i.e. ads are not visible if disabled)
    UserAdvertisingEnabled,
    // Affects all uploads aside from auto generated thumbnails, e.g. games, shirts, ads, etc
    UploadContentEnabled,
    // Following users
    FollowingEnabled,
    // Sending friend reuqests, accepted friend requests, declining
    FriendingEnabled,
    // Sending applications, signup up with application id
    ApplicationsEnabled,
    CreateInvitesEnabled,
    InvitesEnabled,
    AllowAccessToAllRequests,
    ForumsEnabled,
    ForumPostingEnabled,
    CurrencyExchangeEnabled,
    // Features End. Below are fixes.
    UseGameJoinV2,
    SupportTicket,
    AbuseReportsEnabled,
    CreatePlaceSelfService,
    GroupPayoutsEnabled,
    WebsiteChat,
    PasswordReset,
    TradePreventAcceptanceIfTooManyCopies,
    BadgesEnabled,
    CatalogEnabled
}

public static class FeatureFlags
{
    private const string FeatureFlagRedisName = "FeatureFlagsWebV1";
    private static readonly FeatureFlag[] AllFlags = Enum.GetValues<FeatureFlag>();
    private static readonly SemaphoreSlim WriteLock = new(1, 1);
    private static IReadOnlyDictionary<FeatureFlag, bool>? flagsSnapshot;

    public static async Task RefreshOnceAsync()
    {
        await UpdateFlagsAsync();
    }

    private static IReadOnlyDictionary<FeatureFlag, bool> CreateDefaultSnapshot()
    {
        var result = new Dictionary<FeatureFlag, bool>();
        foreach (var flag in AllFlags)
            result[flag] = true;

        return result;
    }

    private static IReadOnlyDictionary<FeatureFlag, bool> NormalizeSnapshot(
        IReadOnlyDictionary<FeatureFlag, bool>? flags)
    {
        var result = new Dictionary<FeatureFlag, bool>();
        foreach (var flag in AllFlags)
            result[flag] = flags?.GetValueOrDefault(flag, true) ?? true;

        return result;
    }

    private static IReadOnlyDictionary<FeatureFlag, bool> DeserializeSnapshot(string? flags)
    {
        if (string.IsNullOrWhiteSpace(flags))
            return CreateDefaultSnapshot();

        var deserialized = JsonSerializer.Deserialize<Dictionary<FeatureFlag, bool>>(flags);
        return NormalizeSnapshot(deserialized);
    }

    internal static IReadOnlyDictionary<FeatureFlag, bool> DeserializeSnapshotForTests(string? flags)
    {
        return DeserializeSnapshot(flags);
    }
    
    internal static void ReplaceSnapshotForTests(IReadOnlyDictionary<FeatureFlag, bool>? flags)
    {
        flagsSnapshot = flags == null ? null : NormalizeSnapshot(flags);
    }

    private static async Task UpdateFlagsAsync()
    {
        var flags = await Cache.distributed.StringGetAsync(FeatureFlagRedisName);
        flagsSnapshot = DeserializeSnapshot(flags);
    }

    private static IReadOnlyDictionary<FeatureFlag, bool> GetSnapshot()
    {
        return flagsSnapshot ?? throw new Exception("Flags are not set");
    }

    public static bool IsEnabled(FeatureFlag flag)
    {
        return GetSnapshot().GetValueOrDefault(flag, true);
    }
    
    public static bool IsDisabled(FeatureFlag flag)
    {
        return !IsEnabled(flag);
    }

    private static async Task SetFlagAsync(FeatureFlag flag, bool enabled)
    {
        await WriteLock.WaitAsync();
        try
        {
            var nextSnapshot = NormalizeSnapshot(GetSnapshot());
            var writableSnapshot = new Dictionary<FeatureFlag, bool>(nextSnapshot)
            {
                [flag] = enabled,
            };
            var normalizedSnapshot = NormalizeSnapshot(writableSnapshot);

            await Roblox.Services.Cache.distributed.StringSetAsync(
                FeatureFlagRedisName,
                JsonSerializer.Serialize(normalizedSnapshot));
            flagsSnapshot = normalizedSnapshot;
        }
        finally
        {
            WriteLock.Release();
        }
    }

    public static Task EnableFlag(FeatureFlag flagToEnable)
    {
        return SetFlagAsync(flagToEnable, true);
    }

    public static Task DisableFlag(FeatureFlag flagToDisable)
    {
        return SetFlagAsync(flagToDisable, false);
    }

    public static IReadOnlyDictionary<FeatureFlag, bool> GetAllFlags()
    {
        return new Dictionary<FeatureFlag, bool>(GetSnapshot());
    }

    public static void FeatureCheck(FeatureFlag flag)
    {
        if (IsDisabled(flag))
        {
            throw new RobloxException(503, 0, "Feature temporarily unavailable");
        }
    }
    
    public static void FeatureCheck(params FeatureFlag[] flags)
    {
        foreach (var item in flags)
        {
            FeatureCheck(item);
        }
    }
}
