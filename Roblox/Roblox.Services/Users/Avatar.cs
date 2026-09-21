using System.Collections.Concurrent;
using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using CsvHelper;
using Dapper;
using Microsoft.VisualBasic;
using Newtonsoft.Json;
using Roblox.Dto;
using Roblox.Dto.Assets;
using Roblox.Dto.Avatar;
using Roblox.Models.Assets;
using Roblox.Models.Avatar;
using Roblox.Services.DbModels;
using Roblox.Exceptions.Services.Users;
using Roblox.Logging;
using Roblox.Rendering;
using Roblox.Services.Exceptions;
using AssetId = Roblox.Services.DbModels.AssetId;
using JsonSerializer = System.Text.Json.JsonSerializer;
using Type = Roblox.Models.Assets.Type;

namespace Roblox.Services;

public class AvatarService : ServiceBase, IService {

    public class AssetTypeGroups
    {
        public int[] clothing { get; set; }
        public int[] accessories { get; set; }
        public int[] avataranimations { get; set; }
        public int[] bodyparts { get; set; }
        public int[] packages { get; set; }
        public int[] all { get; set; }
    }
    
    public AssetTypeGroups recentAssetTypes = new AssetTypeGroups
    {
        clothing = new[]{2, 11, 12},
        accessories = new[]{8,41,42,43,44,45,46,47},
        avataranimations = new[]{48,50,51,52,53,54,55,61,18},
        bodyparts = new[]{17,27,28,29,30,31},
        packages = new[]{31},
        all = new[]{2, 11, 12, 8, 41, 42, 43, 44, 45, 46, 47, 48, 50, 51, 52, 53, 54, 55, 61, 18, 17, 27, 28, 29, 30, 31, 31, 19},
    };
    
    public async Task<IEnumerable<long>> GetWornAssets(long userId)
    {
        // useless inner join is intentional:
        // it's so that we filter out items the user no longer owns.
        return (await db.QueryAsync<AssetId>(
            @"SELECT distinct(av.asset_id) as assetId
                FROM user_avatar_asset av
                INNER JOIN asset a ON a.id = av.asset_id
                LEFT JOIN user_asset ua ON ua.user_id = av.user_id AND ua.asset_id = av.asset_id
                WHERE av.user_id = :user_id
                    AND (ua.asset_id IS NOT NULL OR (a.creator_type = :creator_type AND a.creator_id = :user_id))", new
            {
                user_id = userId,
                creator_type = (int)CreatorType.User,
            })).Select(c => c.assetId);
    }

    public async Task<bool> IsUserAvatar18Plus(long userId)
    {
        var result = await db.QuerySingleOrDefaultAsync<Dto.Total>
        ("SELECT count(*) AS total FROM user_avatar_asset INNER JOIN asset ON asset.id = user_avatar_asset.asset_id WHERE asset.is_18_plus AND user_avatar_asset.user_id = :user_id",
            new
            {
                user_id = userId,
            });
        return result.total != 0;
    }

    public async Task<IEnumerable<long>> GetRecentItems(long userId)
    {
        var result =
            await db.QueryAsync(
                "SELECT distinct asset_id, max(id) FROM user_asset WHERE user_id = :user_id GROUP BY asset_id ORDER BY max(id) DESC, asset_id LIMIT 50",
                new
                {
                    user_id = userId,
                });
        return result.Select(c => (long) c.asset_id);
    }
    public async Task<IEnumerable<long>> GetRecentAvatarItems(long userId, int[] assetTypes)
    {
        var result =
            await db.QueryAsync(
                "SELECT asset_id FROM user_asset ua INNER JOIN asset a ON a.id = ua.asset_id WHERE user_id = :user_id AND a.asset_type = ANY(:types) ORDER BY ua.updated_at DESC LIMIT 25",
                new
                {
                    user_id = userId,
                    types = assetTypes.Select(i => (short)i).ToArray(),
                });
        return result.Select(c => (long) c.asset_id);
    }
    public async Task<AvatarType> GetAvatarType(long userId)
    {
        var result = await db.QuerySingleOrDefaultAsync<AvatarType>(
            "SELECT avatar_type as avatarType FROM user_avatar WHERE user_id = :user_id",
            new
            {
                user_id = userId,
            });

        return result;
    }
    
    public async Task<BodyScales> GetAvatarScales(long userId)
    {
        var avatarScales = await db.QuerySingleOrDefaultAsync<BodyScales>(
            @"SELECT 
                scale_height as height,
                scale_width as width,
                scale_head as head,
                scale_depth as depth,
                scale_proportion as proportion,
                scale_body_type as bodyType
                FROM user_avatar WHERE user_id = :user_id",
            new
            {
                user_id = userId,
            });

        return avatarScales;
    }

    public async Task UpdateRigType(AvatarType type, long userId)
    {
        await db.ExecuteAsync(
            "UPDATE user_avatar SET avatar_type = :avatar_type WHERE user_id = :user_id",
            new
            {
                avatar_type = (int)type,
                user_id = userId,
            });
    }
    
    // DOES NOT CHECK FOR CORRECT SCALES, do it yourself below lal
    public async Task UpdateBodyScales(BodyScales scales, long userId)
    {
        await db.ExecuteAsync(
            @"UPDATE user_avatar SET 
                       scale_height = :height,
                       scale_width = :width,
                       scale_head = :head,
                       scale_depth = :depth,
                       scale_proportion = :proportion,
                       scale_body_type = :bodyType
                  WHERE user_id = :userId",
            new
            {
                userId,
                height = Math.Round(scales.height, 2),
                width = Math.Round(scales.width, 2),
                head = Math.Round(scales.head, 2),
                depth = Math.Round(scales.depth, 2),
                proportion = Math.Round(scales.proportion, 2),
                bodyType = Math.Round(scales.bodyType, 2),
            });
    }
    
    // LIMITS:
    /*
     * HEIGHT: 90-105%
     * WIDTH: 70-100%
     * HEAD: 90-100%
     * DEPTH: same as Width, usually merged actually but is different on API
     * PROPORTION: 0-100%
     * BODYTYPE: 0-100%
     */
    public bool AreScalesValid(BodyScales scales) {

        // i feel like there's a better way to do this but idk lal
        if (scales.height > 1.05 || scales.height < 0.9) return false;
        if (scales.width > 1 || scales.width < 0.7) return false;
        if (scales.head > 1 || scales.head < 0.9) return false;
        if (scales.depth > 1 || scales.depth < 0.7) return false;
        if (scales.proportion > 1 || scales.proportion < 0) return false;
        if (scales.bodyType > 1 || scales.bodyType < 0) return false;

        return true;
    }

    public async Task<AvatarWithColors> GetAvatar(long userId)
    {
        var existingAvatar = await db.QuerySingleOrDefaultAsync<DatabaseAvatarWithImages>(
            "SELECT * FROM user_avatar WHERE user_id = :user_id",
            new
            {
                user_id = userId,
            });
        if (existingAvatar == null)
            throw new RecordNotFoundException();
        return new AvatarWithColors()
        {
            headColorId = existingAvatar.head_color_id,
            torsoColorId = existingAvatar.torso_color_id,
            rightArmColorId = existingAvatar.right_arm_color_id,
            leftArmColorId = existingAvatar.left_arm_color_id,
            rightLegColorId = existingAvatar.right_leg_color_id,
            leftLegColorId = existingAvatar.left_leg_color_id,
            avatarType = existingAvatar.avatar_type,
            thumbnailUrl = existingAvatar.thumbnail_url,
            thumbnail3DUrl = existingAvatar.thumbnail_3d_url,
            headshotUrl = existingAvatar.headshot_thumbnail_url,
            scales = new BodyScales
            {
                width = existingAvatar.scale_width,
                height = existingAvatar.scale_height,
                head = existingAvatar.scale_head,
                depth = existingAvatar.scale_depth,
                proportion = existingAvatar.scale_proportion,
                bodyType = existingAvatar.scale_body_type
            }
        };
    }

    public async Task<ColorEntry> GetAvatarColors(long userId)
    {
        var existingAvatar = await db.QuerySingleOrDefaultAsync<DatabaseAvatar>(
            "SELECT head_color_id, torso_color_id, left_arm_color_id,right_arm_color_id,left_leg_color_id,right_leg_color_id FROM user_avatar WHERE user_id = :user_id",
            new
            {
                user_id = userId,
            });
        return new ColorEntry()
        {
            headColorId = existingAvatar.head_color_id,
            torsoColorId = existingAvatar.torso_color_id,
            rightArmColorId = existingAvatar.right_arm_color_id,
            leftArmColorId = existingAvatar.left_arm_color_id,
            rightLegColorId = existingAvatar.right_leg_color_id,
            leftLegColorId = existingAvatar.left_leg_color_id,
        };
    }

    private readonly Models.Assets.Type[] _wearableAssetTypes = new[]
    {

        Type.Shirt,
        Type.Pants,
        Type.TShirt,
        //accesories
        Type.Face,
        Type.Hat,
        Type.FrontAccessory,
        Type.BackAccessory,
        Type.WaistAccessory,
        Type.HairAccessory,
        Type.NeckAccessory,
        Type.ShoulderAccessory,
        Type.FaceAccessory,

        //body
        Type.LeftArm,
        Type.RightArm,
        Type.LeftLeg,
        Type.RightLeg,
        Type.Torso,
        Type.Head,

        //anims
        Type.ClimbAnimation,
        Type.DeathAnimation,
        Type.FallAnimation,
        Type.IdleAnimation,
        Type.JumpAnimation,
        Type.RunAnimation,
        Type.SwimAnimation,
        Type.WalkAnimation,
        Type.PoseAnimation,
        Type.EmoteAnimation,
        //gears
        Type.Gear,
    };

    /// <summary>
    /// Filter the dirtyAssetIds. This will remove moderated/pending items, items the user doesn't own, invalid items, etc.
    /// </summary>
    /// <param name="userId"></param>
    /// <param name="dirtyAssetIds"></param>
    /// <returns></returns>
    public async Task<IEnumerable<long>> FilterAssetsForRender(long userId, IEnumerable<long> dirtyAssetIds)
    {
        var assetIds = dirtyAssetIds.ToList();
        if (assetIds.Count != 0)
        {
            using var assets = ServiceProvider.GetOrCreate<AssetsService>();
            // Get the moderation status for each item
            var moderationStatus = (await db.QueryAsync<AssetModerationEntry>(
                "SELECT moderation_status as moderationStatus, id as assetId, asset_type as assetType FROM asset WHERE id = ANY(:ids)", new
                {
                    ids = assetIds,
                })).ToList();
            // Duplicate the list so we can mutate it
            var safeModList = moderationStatus.ToList();
            // Add package contents, if required
            foreach (var item in moderationStatus)
            {
                if (item.assetType == Type.Package)
                {
                    var packageAssetIds = await assets.GetPackageAssets(item.assetId);
                    var otherModStatus = (await db.QueryAsync<AssetModerationEntry>(
                        "SELECT moderation_status as moderationStatus, id as assetId, asset_type as assetType FROM asset WHERE id = ANY(:ids)", new
                        {
                            ids = packageAssetIds.ToList(),
                        })).ToList();
                    foreach (var nestedAsset in otherModStatus)
                    {
                        safeModList.Add(nestedAsset);
                        assetIds.Add(nestedAsset.assetId);
                    }
                }
            }
            // Filter items by moderation status - we only want to render ReviewApproved
            assetIds = assetIds.Where(c =>
            {
                var hasEntry = safeModList.Find(v => v.assetId == c);
                if (hasEntry == null) return false;
                if (!_wearableAssetTypes.Contains(hasEntry.assetType)) return false;
                if (hasEntry.moderationStatus != ModerationStatus.ReviewApproved) return false;
                return true;
            }).ToList();
            // Finally, confirm user actually owns each assetId
            // goodAssetIds is a list of assets the user owns
            var goodAssetIds = new List<long>();
            foreach (var id in assetIds)
            {
                var ownResult = await db.QuerySingleOrDefaultAsync<UserAssetEntry>(
                    @"SELECT asset_id as assetId, user_id as userId
                        FROM user_asset
                        WHERE user_id = :user_id AND asset_id = :asset_id
                        UNION
                        SELECT id as assetId, creator_id as userId
                        FROM asset
                        WHERE creator_type = :creator_type AND creator_id = :user_id AND id = :asset_id
                        LIMIT 1",
                    new
                    {
                        user_id = userId,
                        asset_id = id,
                        creator_type = (int)CreatorType.User,
                    });
                if (ownResult != null)
                {
                    goodAssetIds.Add(id);
                }
            }

            assetIds = goodAssetIds;
        }

        return assetIds;
    }

    public string GetAvatarHash(ColorEntry colors, IEnumerable<long> assetVersionIds, BodyScales scales, AvatarType rigType)
    {
        var assets = assetVersionIds.Distinct().ToList();
        assets.Sort((a, b) => a > b ? 1 : a == b ? 0 : -1);
        var str =
            $@"avatar-hash-1.5:
                {string.Join(",", assets)}:
                {colors.headColorId},
                {colors.torsoColorId},
                {colors.leftArmColorId},
                {colors.rightArmColorId},
                {colors.leftLegColorId},
                {colors.rightLegColorId},
                {scales.height},
                {scales.width},
                {scales.depth},
                {scales.proportion},
                {scales.head},
                {scales.bodyType},
                {(int)rigType},
            ";
        var hasher = SHA256.Create();
        var bits = hasher.ComputeHash(Encoding.UTF8.GetBytes(str));
        return Convert.ToHexString(bits).ToLower();
    }

    private async Task<IEnumerable<long>> MultiGetAssetVersionsFromAssetIds(IEnumerable<long> assetIds)
    {
        // todo: make this more efficient :(
        var ids = new List<long>();
        using var assets = ServiceProvider.GetOrCreate<AssetsService>(this);
        foreach (var id in assetIds.Distinct())
        {
            var latest = await assets.GetLatestAssetVersion(id);
            ids.Add(latest.assetVersionId);
        }
        return ids.Distinct();
    }

    public async Task SetWearingAssets(long userId, IEnumerable<long> assetIds)
    {
        var requestedAssetIds = assetIds.Distinct().ToList();

        var tAvatarType = GetAvatarType(userId);
        var tScales = GetAvatarScales(userId);
        var tColors = GetAvatarColors(userId);
        var tFilteredAssets = FilterAssetsForRender(userId, requestedAssetIds);

        await Task.WhenAll(tAvatarType, tScales, tColors, tFilteredAssets);

        var avatarType = await tAvatarType;
        var scales = await tScales;
        var colors = await tColors;
        var filteredAssetIds = (await tFilteredAssets).Distinct().ToList();

        var filteredAssetSet = filteredAssetIds.ToHashSet();
        if (requestedAssetIds.Any(assetId => !filteredAssetSet.Contains(assetId)))
        {
            throw new RobloxException(400, 0, "One or more assets are invalid, moderated, or not owned.");
        }

        if (!AreColorsOk(colors))
            throw new RobloxException(400, 0, "Colors are invalid");
        if (!AreScalesValid(scales) && userId is not (68 or 3))
            throw new RobloxException(400, 0, "Scales are invalid");

        var assetsOk = await ConfirmAssetSelectionIsOkForRender(filteredAssetIds);
        if (!assetsOk)
            throw new RobloxException(400, 0, "One or more assets are invalid");

        await UpdateUserAvatar(userId, colors, filteredAssetIds, scales, avatarType);
        await UpdateUserAvatarImages(userId, null, null, null);
    }

    /// <summary>
    /// Update the userId's avatar. Returns a hash. This does not render or validate anything.
    /// </summary>
    public async Task<string> UpdateUserAvatar(long userId, ColorEntry colors, IEnumerable<long> assetIds,
        BodyScales scales, AvatarType rigType)
    {
        var idsList = assetIds.ToList();
        return await InTransaction(async (trx) =>
        {
            await UpdateAsync("user_avatar", "user_id", userId, new
            {
                head_color_id = colors.headColorId,
                torso_color_id = colors.torsoColorId,
                right_arm_color_id = colors.rightArmColorId,
                left_arm_color_id = colors.leftArmColorId,
                right_leg_color_id = colors.rightLegColorId,
                left_leg_color_id = colors.leftLegColorId,
                scale_height = scales.height,
                scale_width = scales.width,
                scale_head = scales.head,
                scale_depth = scales.depth,
                scale_proportion = scales.proportion,
                scale_body_type = scales.bodyType,
                avatar_type = (int)rigType,
            });
            await db.ExecuteAsync("DELETE FROM user_avatar_asset WHERE user_id = :user_id", new
            {
                user_id = userId,
            });
            foreach (var item in idsList)
            {
                await db.ExecuteAsync("INSERT INTO user_avatar_asset (user_id, asset_id) VALUES (:user_id, :asset_id)",
                    new
                    {
                        user_id = userId,
                        asset_id = item,
                    });
            }

            var assetVersions = await MultiGetAssetVersionsFromAssetIds(idsList);
            return GetAvatarHash(colors, assetVersions, scales, rigType);
        });
    }

    public async Task UpdateUserAvatarImages(long userId, string? headshotImage, string? thumbnailImage, string? thumbnail3DImage)
    {
        await db.ExecuteAsync(
            "UPDATE user_avatar SET thumbnail_url = :thumbnail_url, headshot_thumbnail_url = :headshot_url, thumbnail_3d_url = :thumbnail_3d_url WHERE user_id = :user_id",
            new
            {
                user_id = userId,
                thumbnail_url = thumbnailImage,
                thumbnail_3d_url = thumbnail3DImage,
                headshot_url = headshotImage,
            });
    }

    public async Task<IEnumerable<OutfitEntry>> GetUserOutfits(long userId, int limit, int offset)
    {
        return await db.QueryAsync<OutfitEntry>(
            "SELECT id, name, created_at as created FROM user_outfit WHERE user_id = :user_id ORDER BY id DESC LIMIT :limit OFFSET :offset",
            new
            {
                user_id = userId,
                limit,
                offset,
            });
    }

    public async Task<OutfitExtendedDetails> GetOutfitById(long outfitId)
    {
        var result = await db.QuerySingleOrDefaultAsync<OutfitAvatar>(
            @"SELECT 
                    name,
                    head_color_id as headColorId,
                    torso_color_id as torsoColorId,
                    left_arm_color_id as leftArmColorId,
                    right_arm_color_id as rightArmColorId,
                    left_leg_color_id as leftLegColorId,
                    right_leg_color_id as rightLegColorId,
                    user_id as userId,
                    scale_height as height,
                    scale_width as width,
                    scale_head as head,
                    scale_depth as depth,
                    scale_proportion as proportion,
                    scale_body_type as bodyType,
                    avatar_type as avatarType
                  FROM user_outfit WHERE id = :id",
            new
            {
                id = outfitId,
            });
        var assets =
            (await db.QueryAsync<AssetId>(
                "SELECT asset_id as assetId FROM user_outfit_asset WHERE outfit_id = :outfit_id",
                new {outfit_id = outfitId})).Select(c => c.assetId);

        return new OutfitExtendedDetails()
        {
            assetIds = assets,
            details = result,
        };
    }

    public async Task CreateOutfit(long userId, string name, string? thumbnailUrl, string? headshotUrl,
        OutfitExtendedDetails outfitDetails)
    {
        var existingOutfitCount = await db.QuerySingleOrDefaultAsync<Total>(
            "SELECT COUNT(*) as total FROM user_outfit WHERE user_id = :user_id", new
            {
                user_id = userId,
            });
        if (existingOutfitCount.total >= 100)
            throw new TooManyOutfitsException();
        if (string.IsNullOrEmpty(name) || string.IsNullOrWhiteSpace(name))
            throw new OutfitNameTooShortException();
        if (name.Length > 25)
            throw new OutfitNameTooLongException();
        // image check
        if (string.IsNullOrWhiteSpace(thumbnailUrl) || string.IsNullOrWhiteSpace(headshotUrl))
            throw new NoImageUrlException();

        await InTransaction(async (trx) =>
        {
            var id = await InsertAsync("user_outfit", new
            {
                name = name,
                user_id = userId,
                // colors
                head_color_id = outfitDetails.details.headColorId,
                torso_color_id = outfitDetails.details.torsoColorId,
                left_arm_color_id = outfitDetails.details.leftArmColorId,
                right_arm_color_id = outfitDetails.details.rightArmColorId,
                left_leg_color_id = outfitDetails.details.leftLegColorId,
                right_leg_color_id = outfitDetails.details.rightLegColorId,
                // type
                avatar_type = outfitDetails.details.avatarType,
                // scales
                scale_height = outfitDetails.details.height,
                scale_width = outfitDetails.details.width,
                scale_head = outfitDetails.details.head,
                scale_depth = outfitDetails.details.depth,
                scale_proportion = outfitDetails.details.proportion,
                scale_body_type = outfitDetails.details.bodyType,
                // images
                headshot_thumbnail_url = headshotUrl,
                thumbnail_url = thumbnailUrl,
            });
            foreach (var assetId in outfitDetails.assetIds)
            {
                await InsertAsync("user_outfit_asset", "outfit_id", new
                {
                    outfit_id = id,
                    asset_id = assetId,
                });
            }

            return 0;
        });
    }

    public async Task RenameOutfit(long outfitId, string name) {
        if (string.IsNullOrEmpty(name) || string.IsNullOrWhiteSpace(name))
            throw new OutfitNameTooShortException();
        if (name.Length > 25)
            throw new OutfitNameTooLongException();
        await UpdateAsync("user_outfit", outfitId, new {
            name
        });
    }
    
    public async Task UpdateOutfit(long outfitId, string? name, string? thumbnailUrl, string? headshotUrl,
        OutfitExtendedDetails outfitDetails)
    {
        if (name != null) {
            if (string.IsNullOrEmpty(name) || string.IsNullOrWhiteSpace(name))
                throw new OutfitNameTooShortException();
            if (name.Length > 25)
                throw new OutfitNameTooLongException();
        }
        // image check
        if (string.IsNullOrWhiteSpace(thumbnailUrl) || string.IsNullOrWhiteSpace(headshotUrl))
            throw new NoImageUrlException();
        await InTransaction(async (trx) =>
        {
            await UpdateAsync("user_outfit", outfitId, new
            {
                // colors
                head_color_id = outfitDetails.details.headColorId,
                torso_color_id = outfitDetails.details.torsoColorId,
                left_arm_color_id = outfitDetails.details.leftArmColorId,
                right_arm_color_id = outfitDetails.details.rightArmColorId,
                left_leg_color_id = outfitDetails.details.leftLegColorId,
                right_leg_color_id = outfitDetails.details.rightLegColorId,
                // type
                avatar_type = outfitDetails.details.avatarType,
                // scales
                scale_height = outfitDetails.details.height,
                scale_width = outfitDetails.details.width,
                scale_head = outfitDetails.details.head,
                scale_depth = outfitDetails.details.depth,
                scale_proportion = outfitDetails.details.proportion,
                scale_body_type = outfitDetails.details.bodyType,
                // images
                headshot_thumbnail_url = headshotUrl,
                thumbnail_url = thumbnailUrl,
            });
            if (name != null) {
                // TODO: there's gotta be a better way to do this but idk hjow
                await UpdateAsync("user_outfit", outfitId, new { name });
            }
            await db.ExecuteAsync("DELETE FROM user_outfit_asset WHERE outfit_id = :id", new {id = outfitId});
            foreach (var assetId in outfitDetails.assetIds)
            {
                await InsertAsync("user_outfit_asset", "outfit_id", new
                {
                    outfit_id = outfitId,
                    asset_id = assetId,
                });
            }

            return 0;
        });
    }

    public async Task DeleteOutfit(long outfitId)
    {
        await InTransaction(async (t) =>
        {
            await db.ExecuteAsync("DELETE FROM user_outfit WHERE id = :id", new
            {
                id = outfitId,
            });
            await db.ExecuteAsync("DELETE FROM user_outfit_asset WHERE outfit_id = :outfit_id", new
            {
                outfit_id = outfitId,
            });
            return 0;
        });
    }

    private async Task<bool> ConfirmAssetSelectionIsOkForRender(IEnumerable<long> unknownAssetIds)
    {
        using var assets = ServiceProvider.GetOrCreate<AssetsService>(this);
        var assetIds = unknownAssetIds.ToList();
        if (assetIds.Count == 0) return true;
        var details = await assets.MultiGetInfoById(assetIds);

        // vars
        var gear = 0;
        var face = 0;
        var shirt = 0;
        var pants = 0;
        var tShirt = 0;
        var accessories = 0;
        var leftArm = 0;
        var rightArm = 0;
        var leftLeg = 0;
        var rightLeg = 0;
        var torso = 0;
        var head = 0;
        var animations = 0;
        var emotes = 0;
        var climbAnimation = 0;
        var fallAnimation = 0;
        var idleAnimation = 0;
        var walkAnimation = 0;
        var runAnimation = 0;
        var jumpAnimation = 0;
        var poseAnimation = 0;
        var swimAnimation = 0;

        foreach (var item in details)
        {
            switch (item.assetType)
            {
                case Type.TShirt:
                    tShirt++;
                    break;
                case Type.Shirt:
                    shirt++;
                    break;
                case Type.Pants:
                    pants++;
                    break;
                case Type.Animation:
                    animations++;
                    break;
                case Type.Gear:
                    gear++;
                    break;
                case Type.Face:
                    face++;
                    break;
                case Type.Hat:
                case Type.FrontAccessory:
                case Type.BackAccessory:
                case Type.HairAccessory:
                case Type.NeckAccessory:
                case Type.ShoulderAccessory:
                case Type.WaistAccessory:
                case Type.FaceAccessory:
                    accessories++;
                    break;
                case Type.Head:
                    head++;
                    break;
                case Type.Torso:
                    torso++;
                    break;
                case Type.LeftArm:
                    leftArm++;
                    break;
                case Type.RightArm:
                    rightArm++;
                    break;
                case Type.LeftLeg:
                    leftLeg++;
                    break;
                case Type.RightLeg:
                    rightLeg++;
                    break;
                case Type.FallAnimation:
                    fallAnimation++;
                    break;
                case Type.ClimbAnimation:
                    climbAnimation++;
                    break;
                case Type.IdleAnimation:
                    idleAnimation++;
                    break;
                case Type.WalkAnimation:
                    walkAnimation++;
                    break;
                case Type.RunAnimation:
                    runAnimation++;
                    break;
                case Type.JumpAnimation:
                    jumpAnimation++;
                    break;
                case Type.PoseAnimation:
                    poseAnimation++;
                    break;
                case Type.SwimAnimation:
                    swimAnimation++;
                    break;
                case Type.EmoteAnimation:
                    emotes++;
                    break;
                default:
                    throw new Exception("Unexpected asset type: " + item.assetType);
            }
        }

        if (gear > 1) return false;
        if (tShirt > 1 || shirt > 1 || pants > 1) return false;
        if (face > 1) return false;
        // Orginal is 7
        if (accessories > 15) return false;
        if (leftArm > 1 || rightArm > 1 || leftLeg > 1 || rightLeg > 1 || torso > 1 || head > 1) return false;
        if (emotes > 8) return false;
        if (idleAnimation > 1 || 
            walkAnimation > 1 || 
            runAnimation > 1 || 
            jumpAnimation > 1 || 
            poseAnimation > 1 || 
            swimAnimation > 1 || 
            fallAnimation > 1) 
        {
            return false;
        }

        return true;
    }

    private bool IsColorValid(int color)
    {
        var allColors = Roblox.Models.Avatar.AvatarMetadata.GetColors();
        foreach (var item in allColors)
        {
            if (item.brickColorId == color)
            {
                return true;
            }
        }

        return false;
    }

    public async Task UpdateLastUpdated(long userId, long assetId) {
        await db.QueryAsync("UPDATE user_asset SET updated_at = now() WHERE user_id = :userId AND asset_id = :assetId", new
        {
            userId,
            assetId,
        });
    }

    public bool AreColorsOk(ColorEntry colors)
    {
        if (!IsColorValid(colors.headColorId)) return false;
        if (!IsColorValid(colors.torsoColorId)) return false;
        if (!IsColorValid(colors.leftArmColorId)) return false;
        if (!IsColorValid(colors.rightArmColorId)) return false;
        if (!IsColorValid(colors.leftLegColorId)) return false;
        if (!IsColorValid(colors.rightLegColorId)) return false;
        return true;
    }

    public string GetAvatarRedLockKey(long userId)
    {
        return $"update avatar web v1 {userId}";
    }

    // Tracks last-used time for 3D R2 objects so the cleanup timer can evict stale ones.
    // Keyed by full R2 object key, e.g. "images/thumbnails/3d/{hash}".
    // Resets on app restart — acceptable because the cleanup window is 5 days.
    private static readonly ConcurrentDictionary<string, DateTime> _3dLastUsed = new();

    private static void Touch3DKey(string r2Key) =>
        _3dLastUsed[r2Key] = DateTime.UtcNow;

    /// <summary>
    /// Converts a stored public URL or legacy local path back to its R2 object key.
    /// Handles both "https://cdn.example.com/images/thumbnails/3d/foo"
    /// and the dev-mode form "/images/thumbnails/3d/foo".
    /// </summary>
    private static string UriPathToR2Key(string urlOrPath)
    {
        if (Uri.TryCreate(urlOrPath, UriKind.Absolute, out var uri))
            urlOrPath = uri.AbsolutePath;
        if (urlOrPath.StartsWith('/'))
            urlOrPath = urlOrPath[1..];
        return urlOrPath; // e.g. "images/thumbnails/3d/{file}"
    }

    public async Task Update3DRenderModified(long userId, string avatarHash)
    {
        var thumbnail3DKey = $"images/thumbnails/{avatarHash}_thumbnail3d.json";
        try
        {
            var r2 = ServiceProvider.GetOrCreate<R2StorageService>();
            var jsonStream = await r2.GetFileAsync(thumbnail3DKey);
            if (jsonStream is null)
                throw new FileNotFoundException($"3D thumbnail JSON missing in R2 for hash {avatarHash}.");

            string thumbnail3DJson;
            using (var reader = new StreamReader(jsonStream))
                thumbnail3DJson = await reader.ReadToEndAsync();

            var thumbJson = JsonSerializer.Deserialize<Thumbnail3DRendered>(thumbnail3DJson);
            if (thumbJson is null)
                throw new Exception("Renderer returned incorrect 3D thumbnail.");

            // Refresh last-used timestamps so the cleanup timer doesn't evict these files.
            Touch3DKey(thumbnail3DKey);

            if (!string.IsNullOrEmpty(thumbJson.obj))
                Touch3DKey(UriPathToR2Key(thumbJson.obj));

            if (!string.IsNullOrEmpty(thumbJson.mtl))
                Touch3DKey(UriPathToR2Key(thumbJson.mtl));

            if (thumbJson.textures?.Length > 0)
                foreach (var tex in thumbJson.textures)
                    Touch3DKey(UriPathToR2Key(tex));
        }
        catch (Exception e)
        {
            Console.WriteLine($"[AvatarService] Couldn't touch 3D render entries for AvatarHash {avatarHash}. Error: {e.Message}");
            if (e is FileNotFoundException)
            {
                Console.WriteLine("[AvatarService] Redrawing avatar due to missing 3D render files...");
                await RedrawAvatar(userId);
            }
        }
    }

    public async Task RedrawAvatar(long userId, IEnumerable<long>? newAssetIds = null, ColorEntry? colors = null,
        AvatarType? avatarType = null, bool forceRedraw = false, bool ignoreLock = false,
        bool skipRender = false, BodyScales? scales = null)
    {
        // required services
        using var assets = ServiceProvider.GetOrCreate<AssetsService>();
        var r2 = ServiceProvider.GetOrCreate<R2StorageService>();

        await using var redLock = await Cache.redLock.CreateLockAsync(GetAvatarRedLockKey(userId), TimeSpan.FromMinutes(2));
        if (!redLock.IsAcquired && !ignoreLock) throw new LockNotAcquiredException();

        var tAvatarType = avatarType == null ? GetAvatarType(userId) : Task.FromResult(avatarType.Value);
        var tScales = scales == null ? GetAvatarScales(userId) : Task.FromResult(scales);
        var tColors = colors == null ? GetAvatarColors(userId) : Task.FromResult(colors);
        var tAssets = newAssetIds == null ? GetWornAssets(userId) : Task.FromResult(newAssetIds);

        await Task.WhenAll(tAvatarType, tScales, tColors, tAssets);

        avatarType = await tAvatarType;
        scales = await tScales;
        colors = await tColors;
        var assetIds = (await tAssets).ToList();

        if (!AreColorsOk(colors))
            throw new RobloxException(400, 0, "Colors are invalid");
        if (!AreScalesValid(scales) && userId is not (68 or 3))
            throw new RobloxException(400, 0, "Scales are invalid");

        if (assetIds.Count != 0)
        {
            assetIds = (await FilterAssetsForRender(userId, assetIds)).ToList();
        }

        var assetsOk = await ConfirmAssetSelectionIsOkForRender(assetIds);
        if (!assetsOk)
            throw new RobloxException(400, 0, "One or more assets are invalid");

        // Now, update the avatar. This returns a hash
        var avatarHash = await UpdateUserAvatar(userId, colors, assetIds, scales, avatarType.Value);

        if (skipRender) return;

        var headshotKey = $"images/thumbnails/{avatarHash}_headshot.png";
        var thumbnailKey = $"images/thumbnails/{avatarHash}_thumbnail.png";
        var thumbnail3DKey = $"images/thumbnails/{avatarHash}_thumbnail3d.json";

        if (!forceRedraw)
        {
            var exists = await Task.WhenAll(
                r2.FileExistsAsync(headshotKey),
                r2.FileExistsAsync(thumbnailKey),
                r2.FileExistsAsync(thumbnail3DKey)
            );

            if (exists[0] && exists[1] && exists[2])
            {
                await UpdateUserAvatarImages(userId, headshotKey, thumbnailKey, thumbnail3DKey);
                await Update3DRenderModified(userId, avatarHash);
                return;
            }
        }

        await UpdateUserAvatarImages(userId, null, null, null);

        using var cancellation = new CancellationTokenSource();
        cancellation.CancelAfter(TimeSpan.FromMinutes(2));

        var headshotTask = RenderingHandler.RequestHeadshotThumbnail(userId, cancellation.Token, avatarHash);
        var renderRigType = avatarType == AvatarType.R6 ? AvatarRigType.R6 : AvatarRigType.R15;
        var thumbnailTask = RenderingHandler.RequestPlayerThumbnail(userId, renderRigType, cancellation.Token, avatarHash);
        var thumbnail3DTask = RenderingHandler.RequestPlayerThumbnail3D(userId, cancellation.Token, avatarHash); // Start 3D render early

        try
        {
            await Task.WhenAll(headshotTask, thumbnailTask);
            var headshot = await headshotTask;
            var thumbnail = await thumbnailTask;

            var upload2DTasks = new List<Task>
        {
            ProcessAndUploadImageAsync(r2, headshot, 150, headshotKey),
            ProcessAndUploadImageAsync(r2, thumbnail, 352, thumbnailKey)
        };
            await Task.WhenAll(upload2DTasks);
        }
        catch (Exception ex)
        {
            Writer.Info(LogGroup.AvatarService, "Failed to upload headshot or thumbnail for user {0}: {1}", userId, ex.Message);
            await UpdateUserAvatarImages(userId, null, null, null);
            return;
        }

        await UpdateUserAvatarImages(userId, headshotKey, thumbnailKey, null);

        try
        {
            var thumbnail3DResult = await thumbnail3DTask;
            var thumbJson = JsonSerializer.Deserialize<Thumbnail3DRender>(thumbnail3DResult)
                            ?? throw new Exception("Renderer returned incorrect 3D thumbnail.");

            string? obj = null;
            string? mtl = null;
            var textures = new List<string>();
            var upload3DTasks = new List<Task>();

            async Task Process3DAssetAsync(string contentBase64, string extension, string mimeType, Action<string> setUrl)
            {
                byte[] data = Convert.FromBase64String(contentBase64);
                string hash = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(data)).ToLower();
                string fileName = extension.Contains("tex_") ? $"{hash}_{extension}" : hash;
                var key = $"images/thumbnails/3d/{fileName}";

                setUrl(R2StorageService.GetPublicUrl(key));
                Touch3DKey(key);

                if (!await r2.FileExistsAsync(key))
                {
                    using var ms = new MemoryStream(data);
                    await r2.UploadFileAsync(key, ms, mimeType);
                }
            }

            if (thumbJson.files.TryGetValue("scene.obj", out var sceneObj))
                upload3DTasks.Add(Process3DAssetAsync(sceneObj.content, "obj", "model/obj", url => obj = url));

            if (thumbJson.files.TryGetValue("scene.mtl", out var sceneMtl))
                upload3DTasks.Add(Process3DAssetAsync(sceneMtl.content, "mtl", "model/mtl", url => mtl = url));

            foreach (var (fileName, fileValue) in thumbJson.files)
            {
                if (!fileName.Contains("tex.png", StringComparison.OrdinalIgnoreCase)) continue;

                string baseName = fileName.Replace(".png", "", StringComparison.OrdinalIgnoreCase);
                upload3DTasks.Add(Process3DAssetAsync(fileValue.content, $"tex_{baseName}", "image/png", url => textures.Add(url)));
            }

            await Task.WhenAll(upload3DTasks);

            var thumbnail3DJson = new
            {
                userId,
                thumbJson.camera,
                aabb = new { thumbJson.AABB.min, thumbJson.AABB.max },
                mtl,
                obj,
                textures = textures.Count > 0 ? textures.ToArray() : null
            };

            byte[] jsonBytes = JsonSerializer.SerializeToUtf8Bytes(thumbnail3DJson);
            using (var jsonMs = new MemoryStream(jsonBytes))
            {
                await r2.UploadFileAsync(thumbnail3DKey, jsonMs, "application/json");
            }

            await UpdateUserAvatarImages(userId, headshotKey, thumbnailKey, thumbnail3DKey);
        }
        catch (Exception e)
        {
            Console.WriteLine($"[RewrittenRCC]: Failed to save 3D thumbnail: {e}");
        }
    }

    private async Task ProcessAndUploadImageAsync(R2StorageService r2, string imageResult, int size, string key)
    {
        using var stream = await RenderingHandler.ResizeImage<MemoryStream, string>(imageResult, size, size);
        stream.Position = 0;
        await r2.UploadFileAsync(key, stream, "image/png");
    }
    

    // public async Task TryAsset(long userId, long assetId)
    // {
    // }

    public bool IsThreadSafe()
    {
        return true;
    }

    public bool IsReusable()
    {
        return false;
    }

    public static TimeSpan CleanupStartupDelay => TimeSpan.FromSeconds(3);
    public static TimeSpan CleanupInterval => TimeSpan.FromHours(6);

    public static Task ClearStale3DThumbnailsAsync()
    {
        return Clear3DStaleFiles();
    }

    private static async Task Clear3DStaleFiles()
    {
        Writer.Info(LogGroup.ClearThumbnail3DFolder, "Clearing stale 3D thumbnails from R2 (older than 5 days)...");
        try
        {
            var r2 = ServiceProvider.GetOrCreate<R2StorageService>();
            var keys = await r2.ListFilesAsync("images/thumbnails/3d/");
            var fiveDaysAgo = DateTime.UtcNow.Subtract(TimeSpan.FromDays(5));
            var deleted = 0;

            foreach (var key in keys)
            {
                var lastUsed = _3dLastUsed.GetValueOrDefault(key, DateTime.MinValue);
                if (lastUsed >= fiveDaysAgo) continue;

                if (!_3dLastUsed.ContainsKey(key))
                {
                    var meta = await r2.GetFileMetadataAsync(key);
                    if (meta != null && meta.LastModified.ToUniversalTime() >= fiveDaysAgo)
                    {
                        _3dLastUsed[key] = meta.LastModified;
                        continue;
                    }
                }

                Writer.Info(LogGroup.ClearThumbnail3DFolder,
                    $"Deleting stale 3D thumbnail {key}, last used: {lastUsed:u}");
                await r2.DeleteFileAsync(key);
                _3dLastUsed.TryRemove(key, out var _);
                deleted++;
            }

            Writer.Info(LogGroup.ClearThumbnail3DFolder,
                $"Successfully cleared {deleted} stale 3D thumbnails from R2.");
        }
        catch (Exception ex)
        {
            Writer.Info(LogGroup.ClearThumbnail3DFolder, $"Error during 3D thumbnail cleanup: {ex.Message}");
        }
    }
}
