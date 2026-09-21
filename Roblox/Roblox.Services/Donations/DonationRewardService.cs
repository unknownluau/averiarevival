using Dapper;
using Roblox.Models.Assets;
using Roblox.Models.Economy;

namespace Roblox.Services.Donations;

public sealed class DonationRewardService : ServiceBase, IService
{
    private static readonly DonationTier[] RewardTiers =
    {
        new(50m, 6000, new long[] { /*764757,*/ 957733, 957793, 661866, 957727 }),
        new(25m, 3100, new long[] { 661866 }),
        new(15m, 1750, new long[] { 957793 }),
        new(10m, 1100, new long[] { 957733 }),
        //new(5m, 500, new long[] { 764757 }),
    };

    public DonationTier? GetClampedTier(decimal amount)
    {
        return RewardTiers.FirstOrDefault(tier => amount >= tier.MinimumAmount);
    }

    public async Task<DonationRewardResult> ProcessAsync(DonationRewardRequest request)
    {
        return await InTransaction(async _ =>
        {
            var ledgerId = await db.QuerySingleOrDefaultAsync<long?>(@"
INSERT INTO donation_webhook_event
    (provider, external_event_id, amount, currency, donor_display_name, user_id, status)
VALUES
    (:Provider, :ExternalEventId, :Amount, :Currency, :DonorDisplayName, :UserId, 'processing')
ON CONFLICT (provider, external_event_id) DO NOTHING
RETURNING id
", request);

            if (!ledgerId.HasValue)
                return DonationRewardResult.Duplicate(request);

            var tier = GetClampedTier(request.Amount);
            var skipReason = request.SkipReason;
            if (!string.Equals(request.Currency, "USD", StringComparison.OrdinalIgnoreCase))
                skipReason = "unsupported-currency";
            else if (request.UserId == null && string.IsNullOrWhiteSpace(skipReason))
                skipReason = "username-not-found";
            else if (tier == null)
                skipReason = "below-minimum-tier";

            if (!string.IsNullOrWhiteSpace(skipReason))
            {
                await db.ExecuteAsync(@"
UPDATE donation_webhook_event
SET status = 'skipped', skip_reason = :skipReason, processed_at = CURRENT_TIMESTAMP
WHERE id = :ledgerId
", new { ledgerId, skipReason });

                return DonationRewardResult.Skipped(request, skipReason);
            }

            using var economy = ServiceProvider.GetOrCreate<EconomyService>(this);
            using var users = ServiceProvider.GetOrCreate<UsersService>(this);
            await economy.IncrementCurrency(CreatorType.User, request.UserId!.Value, CurrencyType.Robux, tier!.Robux);

            var alreadyOwnedAssetIds = (await db.QueryAsync<long>(@"
SELECT DISTINCT asset_id
FROM user_asset
WHERE user_id = :userId AND asset_id = ANY(:assetIds)
", new
            {
                userId = request.UserId.Value,
                assetIds = tier.AssetIds.ToArray(),
            })).ToHashSet();

            foreach (var assetId in tier.AssetIds.Where(assetId => !alreadyOwnedAssetIds.Contains(assetId)))
            {
                await users.CreateUserAsset(request.UserId.Value, assetId);
            }

            await db.ExecuteAsync(@"
UPDATE donation_webhook_event
SET status = 'granted', processed_at = CURRENT_TIMESTAMP
WHERE id = :ledgerId
", new { ledgerId });

            return DonationRewardResult.Granted(request, tier);
        });
    }

    public bool IsThreadSafe() => false;

    public bool IsReusable() => true;
}

public sealed record DonationRewardRequest(
    string Provider,
    string ExternalEventId,
    decimal Amount,
    string Currency,
    string? DonorDisplayName,
    long? UserId,
    string? SkipReason = null);

public sealed record DonationRewardResult(
    string Provider,
    string ExternalEventId,
    decimal Amount,
    string Currency,
    string? DonorDisplayName,
    long? UserId,
    string Status,
    string? SkipReason,
    DonationTier? Tier,
    bool IsDuplicate)
{
    public static DonationRewardResult Duplicate(DonationRewardRequest request)
    {
        return FromRequest(request, "duplicate", null, null, true);
    }

    public static DonationRewardResult Skipped(DonationRewardRequest request, string reason)
    {
        return FromRequest(request, "skipped", reason, null, false);
    }

    public static DonationRewardResult Granted(DonationRewardRequest request, DonationTier tier)
    {
        return FromRequest(request, "granted", null, tier, false);
    }

    private static DonationRewardResult FromRequest(
        DonationRewardRequest request,
        string status,
        string? skipReason,
        DonationTier? tier,
        bool isDuplicate)
    {
        return new DonationRewardResult(
            request.Provider,
            request.ExternalEventId,
            request.Amount,
            request.Currency,
            request.DonorDisplayName,
            request.UserId,
            status,
            skipReason,
            tier,
            isDuplicate);
    }
}

public sealed record DonationTier(decimal MinimumAmount, long Robux, IReadOnlyList<long> AssetIds);
