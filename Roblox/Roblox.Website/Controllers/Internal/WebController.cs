using System.ComponentModel.DataAnnotations;
using System.Text;
using System.Web;
using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json;
using Roblox.Dto.Assets;
using Roblox.Exceptions;
using Roblox.Libraries.Assets;
using Roblox.Models.Assets;
using Roblox.Models.Groups;
using Roblox.Models.Staff;
using Roblox.Models.Users;
using Roblox.Services.App.FeatureFlags;
using Roblox.Services.Exceptions;
using Roblox.Website.Filters;
using Roblox.Website.WebsiteModels.Catalog;
using Roblox.Libraries.DiscordApi;
using Roblox.Models.Db;
using DSharpPlus;
using Roblox.Logging;
using DSharpPlus.Entities;
using Roblox.Web.Infrastructure.Metadata;

namespace Roblox.Website.Controllers;

[ApiController]
[Route("/")]
public class WebController : ControllerBase
{
    private static ControllerServices staticServices { get; } = new();
    static WebController()
    {
        Task.Run(async () =>
        {
            while (true)
            {
                try
                {
                    await staticServices.gameServer.DeleteOldGameServers();
                }
                catch (Exception e)
                {
                    Console.WriteLine("[info] KillOldservers task failed: {0}\n{1}", e.Message, e.StackTrace);
                }
                await Task.Delay(TimeSpan.FromSeconds(30));
            }
        });
    }

    [HttpGetBypass("auth/discord-login")]
    public IActionResult DiscordLogin()
    {
        return Redirect($"https://discord.com/oauth2/authorize?client_id={Configuration.DiscordClientId}&response_type=code&redirect_uri={HttpUtility.UrlEncode(Configuration.BaseUrl)}%2Fapi%2Flogincallback&scope=identify+guilds.join");
    }

    [HttpGetBypass("api/logincallback")]
    public async Task<IActionResult> DiscordLoginCallBack(string code)
    {
        if (userSession != null)
            return Redirect("/home");

        var discordApi = await DiscordApi.CreateFromOAuthCode(code, Configuration.DiscordLoginCallback);
        if (discordApi == null)
        {
            return Content("Login via discord has failed, please try logging in normally");
        }
        var userInfo = await discordApi.GetUserInfo();
        if (userInfo == null)
        {
            return Content("Login via discord has failed, please try logging in normally");
        }

        Dto.Users.UserInfo user;
        try
        {
            user = await services.users.GetUserByDiscordId(userInfo.Id.ToString());
        }
        catch (RecordNotFoundException)
        {
            await services.discordBotApi.AddGuildMember(Configuration.DiscordGuildId, userInfo.Id.ToString(), discordApi.AccessToken);
            return Content("We couldn't find a averia account relating to this account, we have automatically joined the Averia discord server for you so you can register an account or link it!");
        }

        var sess = await services.users.CreateSession(user.userId);
        Roblox.Web.Infrastructure.Auth.RobloxSessionCookieWriter.AppendSessionCookies(
            HttpContext,
            sess,
            TimeSpan.FromDays(364));
        return Redirect("/home");
    }

    [HttpGetBypass("api/discordapplicationcallback")]
    public async Task<IActionResult> ApplicationDiscordCallback(string? code)
    {
        const string key = "AVERIA-DISCORD";
        if (discordAccessToken != null)
        {
            HttpContext.Response.Cookies.Delete(key);
        }
        if (code is null)
        {
            return Redirect($"https://discord.com/oauth2/authorize?client_id={Configuration.DiscordClientId}&response_type=code&redirect_uri={Configuration.DiscordApplicationCallback}&scope=identify+guilds.join");
        }
        var discordApi = await DiscordApi.CreateFromOAuthCode(code, Configuration.DiscordApplicationCallback);
        if (discordApi == null)
        {
            return Content("Login via discord has failed, please try logging in normally");
        }

        var userInfo = await discordApi.GetUserInfo();
        if (userInfo == null)
        {
            return Content("Please try again later");
        }

        await services.discordBotApi.AddGuildMember(Configuration.DiscordGuildId, userInfo.Id.ToString(), discordApi.AccessToken);
        string base64AccessToken = Convert.ToBase64String(Encoding.UTF8.GetBytes(discordApi.AccessToken));

        HttpContext.Response.Cookies.Append(key, base64AccessToken, new CookieOptions
        {
            IsEssential = true,
            Path = "/",
            HttpOnly = true,
            Secure = true,
            Expires = DateTimeOffset.Now.Add(TimeSpan.FromSeconds(604800)),
            SameSite = SameSiteMode.Lax,
        });

        return Redirect("/");
    }

    [HttpGet("userads/redirect")]
    public async Task<IActionResult> AdRedirect(string data)
    {
        var decoded = System.Text.Encoding.UTF8.GetString(System.Convert.FromBase64String(data));
        var arr = decoded.Split("|");
        var adId = long.Parse(arr[0]);
        var ad = await services.assets.GetAdvertisementById(adId);
        if (ad.isRunning)
        {
            await services.assets.IncrementAdvertisementClick(ad.id);
        }
        switch (ad.targetType)
        {
            case UserAdvertisementTargetType.Asset:
                var itemData = await services.assets.GetAssetCatalogInfo(ad.targetId);
                var redirectUrl = "/catalog/" + itemData.id + "/" + UrlUtilities.ConvertToSeoName(itemData.name);
                return Redirect(redirectUrl);
            case UserAdvertisementTargetType.Group:
                return Redirect("/My/Groups.aspx?gid=" + ad.targetId);
            default:
                throw new NotImplementedException();
        }
    }

    [HttpGet("/users/favorites/list-json")]
    public async Task<dynamic> GetFavoritesLegacy(long userId, Models.Assets.Type assetTypeId, int pageNumber = 1,
        int itemsPerPage = 10)
    {
        if (pageNumber < 1) pageNumber = 1;
        if (itemsPerPage < 1 || itemsPerPage > 100) itemsPerPage = 10;

        var favs = await services.assets.GetFavoritesOfType(userId, assetTypeId, itemsPerPage,
            (itemsPerPage * pageNumber) - itemsPerPage);
        var details = (await services.assets.MultiGetInfoById(favs.Select(c => c.assetId))).ToList();
        var universeStuff =
            await services.games.MultiGetPlaceDetails(details.Where(c => c.assetType == Models.Assets.Type.Place)
                .Select(c => c.id));

        return new
        {
            IsValid = true,
            Data = new
            {
                Page = pageNumber,
                ItemsPerPage = itemsPerPage,
                PageType = "favorites",
                Items = details.Select(c =>
                {
                    var details = universeStuff.FirstOrDefault(x => x.placeId == c.id);

                    return new
                    {
                        AssetRestrictionIcon = new
                        {
                            CssTag = c.itemRestrictions.Contains("LimitedUnique") ? "limited-unique" :
                                c.itemRestrictions.Contains("Limited") ? "limited" : "",
                        },
                        Item = new
                        {
                            AssetId = c.id,
                            UniverseId = details?.universeId,
                            Name = c.name,
                            AbsoluteUrl = "/catalog/" + c.id + "/--",
                            AssetType = (int)c.assetType,
                            AssetCategory = 0,
                            CurrentVersionId = 0,
                            LastUpdated = (string?)null,
                        },
                        Creator = new
                        {
                            Id = c.creatorTargetId,
                            Name = c.creatorName,
                            Type = (int)c.creatorType,
                            CreatorProfileLink = c.creatorType == CreatorType.Group
                                ? "/My/Groups.aspx?gid=" + c.creatorTargetId
                                : "/users/" + c.creatorTargetId + "/profile",
                        },
                        Product = new
                        {
                            PriceInRobux = c.price,
                            PriceInTickets = c.priceTickets,
                            IsForSale = c.isForSale,
                            Is18Plus = c.is18Plus,
                            IsLimited = c.itemRestrictions.Contains("Limited"),
                            IsLimitedUnique = c.itemRestrictions.Contains("LimitedUnique"),
                            IsFree = c.price == 0,
                        },
                    };
                }),
            },
        };
    }

    [HttpGet("users/inventory/list-json")]
    public async Task<dynamic> GetUserInventoryLegacy(long userId, Models.Assets.Type assetTypeId, string? cursor = "",
        int itemsPerPage = 10)
    {
        var count = await services.inventory.CountInventory(userId, assetTypeId);
        if (count == 0)
        {
            return new
            {
                IsValid = true,
                Data = new
                {
                    TotalItems = 0,
                    nextPageCursor = (string?)null,
                    previousPageCursor = (string?)null,
                    PageType = "inventory",
                    Items = Array.Empty<int>(),
                }
            };
        }

        int offset = !string.IsNullOrWhiteSpace(cursor) ? int.Parse(cursor) : 0;
        int limit = itemsPerPage;
        if (limit is > 100 or < 1) limit = 10;

        var canView = await services.inventory.CanViewInventory(userId, userSession?.userId ?? 0);
        if (!canView)
            return new
            {
                IsValid = false,
                Data = "User does not exist",
            };

        var result = (await services.inventory.GetInventory(userId, assetTypeId, SortOrder.Desc, limit, offset)).ToList();
        var moreAvailable = count > (offset + limit);

        return new
        {
            IsValid = true,
            Data = new
            {
                TotalItems = count,
                Start = 0,
                End = -1,
                Page = ((int)(offset / limit)) + 1,
                nextPageCursor = moreAvailable ? (offset + limit).ToString() : null,
                previousPageCursor = offset >= limit ? (offset - limit).ToString() : null,
                ItemsPerPage = limit,
                PageType = "inventory",
                Items = result.Select(c =>
                {
                    return new
                    {
                        AssetRestrictionIcon = new
                        {
                            CssTag = c.isLimitedUnique ? "limited-unique" : c.isLimited ? "limited" : "",
                        },
                        Item = new
                        {
                            AssetId = c.assetId,
                            UniverseId = (long?)null,
                            Name = c.name,
                            AbsoluteUrl = "/item-item?id=" + c.assetId,
                            AssetType = (int)c.assetTypeId,
                        },
                        Creator = new
                        {
                            Id = c.creatorId,
                            Name = c.creatorName,
                            Type = (int)c.creatorType,
                            CreatorProfileLink = c.creatorType == CreatorType.User
                                ? $"/users/{c.creatorId}/profile"
                                : $"/My/Groups.aspx?gid={c.creatorId}",
                        },
                        Product = new
                        {
                            PriceInRobux = c.originalPrice ?? 0,
                            SerialNumber = c.serialNumber,
                        },
                        PrivateSeller = (object?)null,
                        Thumbnail = new { },
                        UserItem = new { },
                    };
                }),
            },
        };
    }

    [HttpPost("users/set-builders-club")]
    public async Task SetBuildersClub(MembershipType membershipType)
    {
        if (userSession == null || !Enum.IsDefined(membershipType))
            return;

        await services.users.InsertOrUpdateMembership(userSession.userId, membershipType);
    }

    [HttpPost("asset/toggle-profile")]
    public async Task<dynamic> AddAssetToProfile([Required, FromBody] AddToProfileCollectionsRequest request)
    {
        var currentCollection = (await services.inventory.GetCollections(safeUserSession.userId)).ToList();
        if (request.addToProfile)
        {
            var ownsItem = await services.users.GetUserAssets(safeUserSession.userId, request.assetId);
            if (!ownsItem.Any())
                return new
                {
                    isValid = false,
                    data = new { },
                    error = "You do not own this item",
                };

            if (!currentCollection.Contains(request.assetId))
            {
                await services.inventory.SetCollections(safeUserSession.userId, currentCollection.Prepend(request.assetId).Distinct());
            }
        }
        else
        {
            currentCollection.RemoveAll(c => c == request.assetId);
            await services.inventory.SetCollections(safeUserSession.userId, currentCollection);
        }

        return new
        {
            isValid = true,
            data = new { },
            error = "",
        };
    }

    [HttpGet("places/{placeId}/settings")]
    public async Task<dynamic> GetPlaceSettings(long placeId)
    {
        var place = await services.assets.GetAssetCatalogInfo(placeId);
        return new
        {
            Creator = new
            {
                Name = place.creatorName,
                CreatorType = (int)place.creatorType,
                CreatorTargetId = place.creatorTargetId,
            }
        };
    }

    [HttpGet("users/profile/robloxcollections-json")]
    public async Task<dynamic> GetUserCollections(long userId)
    {
        var result = (await services.inventory.GetCollections(userId)).ToList();
        if (result.Count < 1)
        {
            var inventory = await services.inventory.GetInventory(userId, Models.Assets.Type.Hat, SortOrder.Desc, 6, 0);
            result = inventory.Take(6).Select(c => c.assetId).ToList();
        }
        var items = (await services.assets.MultiGetInfoById(result)).ToArray();
        var thumbnails = await services.thumbnails.GetAssetThumbnails(result);
        return new
        {
            CollectionsItems = result.Select(id =>
            {
                var c = items.First(i => i.id == id);
                var t = thumbnails.First(d => d.targetId == id);
                return new
                {
                    Id = c.id,
                    AssetSeoUrl = $"/item-item?id=" + c.id,
                    Name = c.name,
                    FormatName = (string?)null,
                    Thumbnail = new
                    {
                        Final = true,
                        Url = t.imageUrl ?? "/img/blocked.png",
                        Id = t.targetId,
                    },
                    AssetRestrictionIcon = new
                    {
                        TooltipText = (string?)null,
                        CssTag = c.itemRestrictions.Contains("Limited") ? "limited" :
                            c.itemRestrictions.Contains("LimitedUnique") ? "limited-unique" : null,
                        LoadAssetRestrictionIconCss = false,
                        HasTooltip = false,
                    },
                };
            }),
        };
    }

    [HttpGet("comments/get-json")]
    public async Task<dynamic> GetAssetComments(long assetId, int startIndex)
    {
        FeatureFlags.FeatureCheck(FeatureFlag.AssetCommentsEnabled);
        var com = await services.assets.GetComments(assetId, startIndex, 10);
        var isModerator = userSession != null && (await services.users.GetStaffPermissions(userSession.userId))
            .Any(a => a.permission == Access.DeleteComment);
        var thumbnails = await services.thumbnails.GetUserThumbnails(com.Select(c => c.userId).Distinct().ToList());
        return new
        {
            IsUserModerator = isModerator,
            MaxRows = 10,
            AreCommentsDisabled = false,
            Comments = com.Select(c =>
            {
                var t = thumbnails.First(d => d.targetId == c.userId);
                return new
                {
                    Id = c.id,
                    PostedDate = c.createdAt.ToString("MMM").Replace(".", "") + c.createdAt.ToString(" dd, yyyy | h:mm ") + c.createdAt.ToString("tt").ToUpper().Replace(".", ""),
                    AuthorName = c.username,
                    AuthorId = c.userId,
                    Text = c.comment,
                    ShowAuthorOwnsAsset = false,
                    AuthorThumbnail = new
                    {
                        AssetId = 0,
                        AssetHash = (string?)null,
                        AssetTypeId = 0,
                        Url = t.imageUrl ?? "/img/blocked.png",
                        IsFinal = true,
                    },
                };
            })
        };
    }

    [HttpPost("comments/post")]
    public async Task<dynamic> AddComment([Required, FromBody] AddCommentRequest request)
    {
        FeatureFlags.FeatureCheck(FeatureFlag.AssetCommentsEnabled);
        try
        {
            await services.assets.AddComment(request.assetId, safeUserSession.userId, request.text);
            return new
            {
                ErrorCode = (string?)null,
            };
        }
        catch (ArgumentException e)
        {
            return new
            {
                ErrorCode = e.Message,
            };
        }
    }

    [HttpPost("game/get-join-script")]
    [RequireRobloxSession]
    [RequireRobloxCsrf]
    public async Task<dynamic> GetJoinScript(long placeId)
    {
        FeatureFlags.FeatureCheck(FeatureFlag.GamesEnabled);
        string clientVer;
        long year = await services.games.GetYear(placeId);
        clientVer = services.games.clientVersionMap.TryGetValue(year, out var ver) ? ver : throw new BadRequestException();
        var assetInfo = (await services.assets.MultiGetAssetDeveloperDetails(new[] { placeId })).First();
        if (assetInfo.moderationStatus != ModerationStatus.ReviewApproved || assetInfo.typeId != (int)Models.Assets.Type.Place)
            throw new BadRequestException(1, "Place is not active");
        var negotiationTicket = await services.sessionNegotiationTickets.IssueAsync(AVERISECURITY!, GetIP());
        var bootstrapperArgs = $":1+launchmode:play+clientversion:{clientVer}+gameinfo:{negotiationTicket}+placelauncherurl:{Configuration.BaseUrl}/Game/PlaceLauncher.ashx?request=RequestGame&placeId={placeId}&isPartyLeader=false&gender=&isTeleport=true+k:l+client";
        var args =
            @$"--authenticationUrl {Roblox.Configuration.BaseUrl}/Login/Negotiate.ashx 
            --authenticationTicket {negotiationTicket} 
            --joinScriptUrl {Configuration.BaseUrl}/Game/PlaceLauncher.ashx?request=RequestGame&placeId={placeId}&isPartyLeader=false&gender=&isTeleport=true";
        return new
        {
            joinScriptUrl = bootstrapperArgs,
            prefix = "averia-player",
            retroArgs = args
        };
    }

    [HttpPost("game/get-join-script-fromjobid")]
    [RequireRobloxSession]
    [RequireRobloxCsrf]
    public async Task<dynamic> GetJoinScriptFromJobId(long placeId, string jobId)
    {
        FeatureFlags.FeatureCheck(FeatureFlag.GamesEnabled);
        string clientVer;
        long year = await services.games.GetYear(placeId);
        clientVer = services.games.clientVersionMap.TryGetValue(year, out var ver) ? ver : throw new BadRequestException();

        var placeInfo = await services.assets.GetAssetCatalogInfo(placeId);
        if (placeInfo.assetType != Models.Assets.Type.Place) throw new BadRequestException();
        var modInfo = (await services.assets.MultiGetAssetDeveloperDetails(new[] { placeId })).First();
        if (modInfo.moderationStatus != ModerationStatus.ReviewApproved) throw new BadRequestException();
        var negotiationTicket = await services.sessionNegotiationTickets.IssueAsync(AVERISECURITY!, GetIP());
        var bootstrapperArgs = $":1+launchmode:play+clientversion:{clientVer}+gameinfo:{negotiationTicket}+placelauncherurl:{Configuration.BaseUrl}/Game/PlaceLauncher.ashx?request=RequestGameJob&placeId={placeId}&gameId={jobId}&isPartyLeader=false&gender=&isTeleport=true+k:l+client";
        var args =
            $"--authenticationUrl {Roblox.Configuration.BaseUrl}/Login/Negotiate.ashx --authenticationTicket {negotiationTicket} --joinScriptUrl {Configuration.BaseUrl}/Game/PlaceLauncher.ashx?request=RequestGameJob&placeId={placeId}&gameId={jobId}&isPartyLeader=false&gender=&isTeleport=true";
        return new
        {
            joinScriptUrl = bootstrapperArgs,
            prefix = "averia-player",
            retroArgs = args
        };
    }

    [HttpGet("usercheck/show-tos")]
    public dynamic GetIsTosCheckRequired()
    {
        return new
        {
            success = true,
        };
    }

    [HttpPostBypass("ide/places/createV2")]
    public async Task<dynamic> CreatePlaceInUniverse(long templatePlaceIdToUse, long universeId)
    {
        if (!await services.cooldown.TryCooldownCheck($"CreatePlaceInUniverse:{safeUserSession.userId}", TimeSpan.FromSeconds(5)))
            throw new BadRequestException(1, "You are creating places too fast, please wait a few seconds before trying again");
        await services.games.CanManageUniverse(safeUserSession.userId, universeId);

        if (await services.games.CountUniversePlaces(universeId) >= 10)
            throw new BadRequestException(1, "You cannot create more than 10 places in a universe");
        var place = await services.games.CreatePlaceInUniverse(safeUserSession.userId, safeUserSession.username, CreatorType.User, universeId);
        return new
        {
            PlaceId = place.placeId,
        };
    }

    [HttpGet("games/getgameinstancesjson")]
    public async Task<dynamic> GetGameServers(long placeId, int startIndex)
    {
        var limit = 10;
        var offset = startIndex;
        var servers = (await services.gameServer.GetGameServers(placeId, offset, limit)).ToList();
        var details = (await services.games.MultiGetPlaceDetails(new[] { placeId })).First();
        List<dynamic> collection = new List<dynamic>();

        servers = servers.OrderByDescending(s => s.players.Count()).ToList();
        foreach (var server in servers)
        {
            var players = server.players.ToList();
            long ping = await services.gameServer.GetServerStat(server.id);

            collection.Add(new
            {
                placeId,
                Capacity = details.maxPlayerCount,
                Ping = ping,
                Fps = 60,
                ShowSlowGameMessage = ping > 200,
                UserCanJoin = true,
                ShowShutdownButton = details.builderId == safeUserSession.userId,
                jobId = server.id,
                FriendsMouseover = "",
                FriendsDescription = "",
                PlayersCapacity = $"{players.Count} of {details.maxPlayerCount}",
                RobloxAppJoinScript = "",
                CurrentPlayers = players.Select(c => new
                {
                    Id = c.userId,
                    Username = c.username,
                    Thumbnail = new
                    {
                        IsFinal = true,
                        Url = "/Thumbs/Avatar-Headshot.ashx?userid=" + c.userId
                    }
                })
            });
        }
        return new
        {
            PlaceId = placeId,
            ShowShutdownAllButton = details.builderId == safeUserSession.userId,
            Collection = collection,
            TotalCollectionSize = servers.Count,
        };
    }

    [HttpGet("search/users/results")]
    public async Task<dynamic> SearchUsersJson(string? keyword = null, int offset = 0, int limit = 10)
    {
        if (limit is > 100 or < 1)
            limit = 10;
        if ((offset / limit) > 1000)
            offset = 0;
        bool exactMatch = false;
        string exactName = string.Empty;
        if (!string.IsNullOrWhiteSpace(keyword) && keyword.StartsWith("@") && keyword.EndsWith("@") && keyword.Length > 2)
        {
            exactMatch = true;
            exactName = keyword.Substring(1, keyword.Length - 2);
        }
        if (exactMatch)
        {
            var user = await services.users.GetUserByName(exactName);
            if (user == null)
            {
                return new
                {
                    Keyword = keyword,
                    StartIndex = offset,
                    MaxRows = limit,
                    TotalResults = 0,
                    UserSearchResults = Array.Empty<int>(),
                };
            }

            var presence = (await services.users.MultiGetPresence(new List<long> { user.userId })).First();

            return new
            {
                Keyword = keyword,
                StartIndex = offset,
                MaxRows = limit,
                TotalResults = 1,
                UserSearchResults = new[]
                {
                    new
                    {
                        UserId = user.userId,
                        Name = user.username,
                        DisplayName = user.username,
                        Blurb = user.description,
                        PreviousUserNamesCsv = "",
                        IsOnline = presence != null && presence.userPresenceType != PresenceType.Offline,
                        LastLocation = presence?.lastLocation,
                        LastSeenDate = presence?.lastOnline,
                        UserProfilePageUrl = "/users/" + user.userId + "/profile",
                        PrimaryGroup = "",
                        PrimaryGroupUrl = "",
                    }
                },
            };
        }
        var result = (await services.users.SearchUsers(keyword, limit, offset)).ToArray();
        if (result.Length == 0)
            return new
            {
                Keyword = keyword,
                StartIndex = offset,
                MaxRows = limit,
                TotalResults = 0,
                UserSearchResults = Array.Empty<int>(),
            };
        var userInfo = await services.users.MultiGetUsersById(result.Skip(offset).Take(limit).Select(c => c.userId));
        var userPresence = await services.users.MultiGetPresence(userInfo.Select(c => c.id).ToList());

        return new
        {
            Keyword = keyword,
            StartIndex = offset,
            MaxRows = limit,
            TotalResults = result.Length,
            UserSearchResults = userInfo.Select(c =>
            {
                var presence = userPresence.FirstOrDefault(p => p.userId == c.id);
                return new
                {
                    UserId = c.id,
                    Name = c.name,
                    DisplayName = c.displayName,
                    Blurb = c.description,
                    PreviousUserNamesCsv = "",
                    IsOnline = presence != null && presence.userPresenceType != PresenceType.Offline,
                    LastLocation = presence?.lastLocation,
                    LastSeenDate = presence?.lastOnline,
                    UserProfilePageUrl = "/users/" + c.id + "/profile",
                    PrimaryGroup = "",
                    PrimaryGroupUrl = "",
                };
            }),
        };
    }

    private static readonly List<Models.Assets.Type> AllowedAssetTypes = new()
    {
        Models.Assets.Type.Audio,
        Models.Assets.Type.TShirt,
        Models.Assets.Type.Shirt,
        Models.Assets.Type.Pants,
        Models.Assets.Type.Image,
        Models.Assets.Type.Video,
        Models.Assets.Type.Mesh,
        Models.Assets.Type.Animation,
        Models.Assets.Type.Model,
        Models.Assets.Type.GamePass,
        Models.Assets.Type.Badge
    };

    private static int pendingAssetUploads { get; set; } = 0;
    private static readonly SemaphoreSlim pendingAssetUploadsMux = new(1, 1);

    [HttpPost("develop/upload-version")]
    public async Task UploadVersion([Required, FromForm] UploadAssetVersionRequest request)
    {
        FeatureFlags.FeatureCheck(FeatureFlag.UploadContentEnabled);
        var info = await services.assets.GetAssetCatalogInfo(request.assetId);
        var canUpload = await services.assets.CanUserModifyItem(info.id, safeUserSession.userId);

        if (info.assetType != Models.Assets.Type.Place)
            canUpload = false;

        if (!canUpload)
            throw new RobloxException(403, 0, "Unauthorized");

        await pendingAssetUploadsMux.WaitAsync();
        try
        {
            if (pendingAssetUploads >= 2)
                throw new RobloxException(429, 0, "TooManyRequests");
            pendingAssetUploads++;
        }
        finally
        {
            pendingAssetUploadsMux.Release();
        }

        try
        {
            using var fs = request.file.OpenReadStream();
            fs.Position = 0;
            using var validationStream = new MemoryStream();
            using var placeStream = new MemoryStream();
            await fs.CopyToAsync(validationStream);
            validationStream.Position = 0;
            await validationStream.CopyToAsync(placeStream);
            validationStream.Position = 0;

            if (!await services.assets.ValidateAssetFile(validationStream, info.assetType))
                throw new RobloxException(400, 0, "The asset file doesn't look correct. Please try again.");
            placeStream.Position = 0;
            await services.assets.CreateAssetVersion(request.assetId, safeUserSession.userId, placeStream);
        }
        finally
        {
            await pendingAssetUploadsMux.WaitAsync();
            try
            {
                pendingAssetUploads--;
            }
            finally
            {
                pendingAssetUploadsMux.Release();
            }
        }
    }

    [HttpPost("develop/upload")]
    public async Task<CreateResponse> UploadItem([Required, FromForm] UploadAssetRequest request)
    {
        FeatureFlags.FeatureCheck(FeatureFlag.UploadContentEnabled);

        if (!AllowedAssetTypes.Contains(request.assetType) || userSession == null)
            throw new BadRequestException(0, "Asset type not supported");

        if (!await services.cooldown.TryIncrementBucketCooldown("Develop:Upload:UserId:" + userSession.userId, 10, TimeSpan.FromMinutes(1))
            || !await services.cooldown.TryIncrementBucketCooldown("Develop:Upload:Ip:" + GetIP(), 10, TimeSpan.FromMinutes(1)))
            throw new RobloxException(429, 0, "Too many requests");

        /*var pendingAssets = await services.assets.CountAssetsPendingApproval();
        if (pendingAssets >= 150)
        {
            Metrics.UserMetrics.ReportFloodCheck(Metrics.UserFloodCheckType.PendingAsset, Metrics.FloodCheckScope.Global);
            throw new RobloxException(400, 0, "There are too many pending items. Try again in a few minutes.");
        }*/

        var groupId = request.groupId ?? 0;
        var creatorType = groupId == 0 ? CreatorType.User : CreatorType.Group;
        var creatorId = creatorType == CreatorType.User ? userSession.userId : groupId;

        if (creatorType == CreatorType.Group)
        {
            var hasPermission = await services.groups.DoesUserHavePermission(userSession.userId, groupId, GroupPermission.CreateItems);
            if (!hasPermission)
                throw new RobloxException(401, 0, "Unauthorized");
        }

        var myPendingItems = await services.assets.CountAssetsByCreatorPendingApproval(groupId, CreatorType.Group);
        if (myPendingItems >= 20)
        {
            Metrics.UserMetrics.ReportFloodCheck(Metrics.UserFloodCheckType.PendingAsset, Metrics.FloodCheckScope.Local);
            throw new RobloxException(409, 0, "You have uploaded too many items in a short period of time. Wait a few minutes and try again.");
        }

        await pendingAssetUploadsMux.WaitAsync();
        try
        {
            if (pendingAssetUploads >= 5)
            {
                Metrics.UserMetrics.ReportFloodCheck(Metrics.UserFloodCheckType.Upload, Metrics.FloodCheckScope.Global);
                throw new RobloxException(409, 0, "There are too many pending assets at this time. Try again in a few minutes.");
            }
            pendingAssetUploads++;
        }
        finally
        {
            pendingAssetUploadsMux.Release();
        }

        await using var stream = request.file.OpenReadStream();

        try
        {
            return request.assetType switch
            {
                Models.Assets.Type.Shirt or Models.Assets.Type.Pants or Models.Assets.Type.TShirt =>
                    await UploadClothing(request, stream, creatorId, creatorType),
                Models.Assets.Type.Audio => await UploadAudio(request, stream, creatorId, creatorType),
                Models.Assets.Type.Image => await UploadImage(request, stream, creatorId, creatorType),
                Models.Assets.Type.Video => await UploadVideo(request, stream, creatorId, creatorType),
                Models.Assets.Type.Mesh => await UploadMesh(request, stream, creatorId, creatorType),
                Models.Assets.Type.MeshPart => await UploadMeshPart(request, stream, creatorId, creatorType),
                Models.Assets.Type.Model => await UploadModel(request, stream, creatorId, creatorType),
                Models.Assets.Type.GamePass => await UploadGamePass(request, stream, creatorId, creatorType),
                Models.Assets.Type.Badge => await UploadAssetBadge(request, stream, creatorId, creatorType),
                Models.Assets.Type.Animation => await UploadAnimation(request, stream, creatorId, creatorType),
                _ => throw new RobloxException(RobloxException.BadRequest, 0, "Endpoint does not support this assetType: " + request.assetType)
            };
        }
        finally
        {
            await pendingAssetUploadsMux.WaitAsync();
            try
            {
                pendingAssetUploads--;
            }
            finally
            {
                pendingAssetUploadsMux.Release();
            }
        }
    }

    private async Task<byte[]> ReadStreamToByteArray(Stream stream)
    {
        stream.Position = 0;
        byte[] buffer = new byte[stream.Length];
        await stream.ReadExactlyAsync(buffer, 0, buffer.Length);
        return buffer;
    }

    private MemoryStream CreateStreamFromBytes(byte[] data)
    {
        return new MemoryStream(data);
    }

    private async Task<CreateResponse> UploadClothing(UploadAssetRequest request, Stream stream, long creatorId, CreatorType creatorType)
    {
        var pictureData = await services.assets.ValidateClothing(stream, request.assetType);
        if (pictureData == null)
            throw new BadRequestException(0, "Invalid image file");

        stream.Position = 0;
        using var cleanImage = await services.assets.CleanImage(stream);
        byte[] imageBytes = await ReadStreamToByteArray(cleanImage);

        var hashStream = CreateStreamFromBytes(imageBytes);
        var imageHash = await services.assets.GenerateImageHash(hashStream);
        hashStream.Dispose();

        var createAssetStream = CreateStreamFromBytes(imageBytes);
        var imageAsset = await services.assets.CreateAsset(
            request.file.FileName,
            request.assetType + " Image",
            safeUserSession.userId,
            creatorType,
            creatorId,
            createAssetStream,
            Models.Assets.Type.Image,
            Genre.All,
            ModerationStatus.AwaitingApproval);
        createAssetStream.Dispose();

        await services.assets.InsertOrUpdateAssetVersionMetadataImage(
            imageAsset.assetVersionId,
            imageBytes.Length,
            pictureData.width,
            pictureData.height,
            pictureData.imageFormat,
            imageHash);

        var clothingAsset = await services.assets.CreateAsset(
            request.name,
            null,
            safeUserSession.userId,
            creatorType,
            creatorId,
            null,
            request.assetType,
            Genre.All,
            imageAsset.moderationStatus,
            default,
            default,
            default,
            default,
            imageAsset.assetId);

        await services.users.CreateUserAsset(safeUserSession.userId, clothingAsset.assetId);

        return clothingAsset;
    }
    
    private async Task<CreateResponse> UploadAudio(UploadAssetRequest request, Stream stream, long creatorId, CreatorType creatorType)
    {
        var balance = await services.economy.GetBalance(creatorType, creatorId);
        if (balance.robux < 20)
            throw new BadRequestException(0, "Not enough Robux for purchase");

        byte[] audioBytes = await ReadStreamToByteArray(stream);

        var validationStream = CreateStreamFromBytes(audioBytes);
        var status = await Services.AudioService.IsAudioValid(validationStream, creatorId);
        validationStream.Dispose();

        if (status == MediaValidation.UnsupportedFormat)
            throw new BadRequestException(0, "Unsupported audio format, try uploading as an MP3 instead");

        if (status == MediaValidation.TooLoud)
            throw new BadRequestException(0, "Audio is too loud. Please keep the peak level at or below -2 dB.");

        if (status != MediaValidation.Ok)
            throw new BadRequestException(0, $"Bad audio file. Error code: {status.ToString()}");

        await services.economy.ChargeForAudioUpload(creatorType, creatorId);
        var createStream = CreateStreamFromBytes(audioBytes);
        var asset = await services.assets.CreateAsset(request.name, null, safeUserSession.userId, CreatorType.User,
            safeUserSession.userId, createStream, Models.Assets.Type.Audio, Genre.All, ModerationStatus.AwaitingApproval);
        createStream.Dispose();

        return asset;
    }

    private async Task<CreateResponse> UploadImage(UploadAssetRequest request, Stream stream, long creatorId, CreatorType creatorType)
    {
        var imageData = await services.assets.ValidateImage(stream);
        if (imageData == null)
            throw new BadRequestException(0, "Invalid image file");

        stream.Position = 0;
        using var cleanImage = await services.assets.CleanImage(stream);
        byte[] imageBytes = await ReadStreamToByteArray(cleanImage);

        var hashStream = CreateStreamFromBytes(imageBytes);
        var imageHash = await services.assets.GenerateImageHash(hashStream);
        hashStream.Dispose();

        var createAssetStream = CreateStreamFromBytes(imageBytes);
        var imageAsset = await services.assets.CreateAsset(request.name, "Image",
            safeUserSession.userId, creatorType, creatorId, createAssetStream, Models.Assets.Type.Image,
            Genre.All,
            ModerationStatus.AwaitingApproval);
        createAssetStream.Dispose();

        await services.assets.InsertOrUpdateAssetVersionMetadataImage(imageAsset.assetVersionId, imageBytes.Length,
            imageData.width, imageData.height, imageData.imageFormat, imageHash);

        return imageAsset;
    }

    private async Task<CreateResponse> UploadAssetBadge(UploadAssetRequest request, Stream stream, long creatorId, CreatorType creatorType)
    {
        if (request.universeId is null)
            throw new BadRequestException(0, "Universe ID is required");

        long universeId = (long)request.universeId;
        var universe = await services.games.SafeGetUniverseInfo(safeUserSession.userId, universeId);
        await services.assets.ValidatePermissions(universe.rootPlaceId, safeUserSession.userId);
        var imageData = await services.assets.ValidateImage(stream);
        if (imageData == null)
            throw new BadRequestException(0, "Invalid image file");

        var badgeCount = await services.games.GetUniverseBadgeCount(universeId);
        if (badgeCount >= 500)
            throw new BadRequestException(0, "This universe has too many badges");

        stream.Position = 0;
        using var cleanImage = await services.assets.CleanImage(stream);
        byte[] imageBytes = await ReadStreamToByteArray(cleanImage);

        var hashStream = CreateStreamFromBytes(imageBytes);
        var imageHash = await services.assets.GenerateImageHash(hashStream);
        hashStream.Dispose();

        var createAssetStream = CreateStreamFromBytes(imageBytes);
        var badgeAsset = await services.assets.CreateAsset(request.name, request.description,
            safeUserSession.userId, creatorType, creatorId, createAssetStream, Models.Assets.Type.Badge,
            Genre.All,
            ModerationStatus.AwaitingApproval);
        createAssetStream.Dispose();

        await services.assets.InsertOrUpdateAssetVersionMetadataImage(badgeAsset.assetVersionId, imageBytes.Length,
            420, 420, imageData.imageFormat, imageHash);
        await services.assets.CreateBadgeAsset(badgeAsset.assetId, request.universeId);
        await services.assets.UpdateAssetMarketInfo(badgeAsset.assetId, false, false, false, null, null);

        return badgeAsset;
    }

    private async Task<CreateResponse> UploadGamePass(UploadAssetRequest request, Stream stream, long creatorId, CreatorType creatorType)
    {
        if (request.universeId is null)
            throw new BadRequestException(0, "Universe ID is required");

        if (request.priceInRobux is null && request.priceInTickets is null && request.isForSale == true)
            throw new BadRequestException(0, "A price is required");

        var imageData = await services.assets.ValidateImage(stream);
        if (imageData == null)
            throw new BadRequestException(0, "Invalid image file");

        long universeId = (long)request.universeId;

        var universe = await services.games.SafeGetUniverseInfo(safeUserSession.userId, universeId);
        await services.assets.ValidatePermissions(universe.rootPlaceId, safeUserSession.userId);

        var gamePassCount = await services.games.GetUniverseGamePassCount(universeId);
        if (gamePassCount >= 15)
            throw new BadRequestException(0, "This universe has too many gamepasses");

        stream.Position = 0;
        using var cleanImage = await services.assets.CleanImage(stream);
        byte[] imageBytes = await ReadStreamToByteArray(cleanImage);

        var hashStream = CreateStreamFromBytes(imageBytes);
        var imageHash = await services.assets.GenerateImageHash(hashStream);
        hashStream.Dispose();

        var createAssetStream = CreateStreamFromBytes(imageBytes);
        var gamepassAsset = await services.assets.CreateAsset(request.name, request.description,
            safeUserSession.userId, creatorType, creatorId, createAssetStream, Models.Assets.Type.GamePass,
            Genre.All,
            ModerationStatus.AwaitingApproval);
        createAssetStream.Dispose();

        await services.assets.InsertOrUpdateAssetVersionMetadataImage(gamepassAsset.assetVersionId, imageBytes.Length,
            imageData.width, imageData.height, imageData.imageFormat, imageHash);
        await services.assets.CreateGamePassAsset(gamepassAsset.assetId, universe.id);
        await services.assets.UpdateAssetMarketInfo(gamepassAsset.assetId, request.isForSale == true, false, false, null, null);
        await services.assets.SetItemPrice(gamepassAsset.assetId, request.priceInRobux, request.priceInTickets);

        return gamepassAsset;
    }

    private async Task<CreateResponse> UploadVideo(UploadAssetRequest request, Stream stream, long creatorId, CreatorType creatorType)
    {
        var balance = await services.economy.GetBalance(creatorType, creatorId);
        if (balance.robux < 100)
            throw new BadRequestException(0, "Not enough Robux for purchase");

        byte[] videoBytes = await ReadStreamToByteArray(stream);

        var validationStream = CreateStreamFromBytes(videoBytes);
        var isOk = await services.assets.IsVideoValid(validationStream);
        validationStream.Dispose();

        if (isOk != MediaValidation.Ok)
            throw new BadRequestException(0, "Bad video file. Error = " + isOk.ToString());

        await services.economy.ChargeForVideoUpload(creatorType, creatorId);
        var createStream = CreateStreamFromBytes(videoBytes);
        var asset = await services.assets.CreateAsset(request.name, null, safeUserSession.userId, CreatorType.User,
            safeUserSession.userId, createStream, Models.Assets.Type.Video, Genre.All, ModerationStatus.AwaitingApproval);
        createStream.Dispose();

        return asset;
    }

    private async Task<CreateResponse> UploadMesh(UploadAssetRequest request, Stream stream, long creatorId, CreatorType creatorType)
    {
        byte[] meshBytes = await ReadStreamToByteArray(stream);

        var validationStream = CreateStreamFromBytes(meshBytes);
        if (!await services.assets.IsMeshValid(validationStream))
        {
            validationStream.Dispose();
            throw new BadRequestException(0, "Bad mesh file");
        }
        validationStream.Dispose();

        var createStream = CreateStreamFromBytes(meshBytes);
        var asset = await services.assets.CreateAsset(request.name, null, creatorId, creatorType,
            safeUserSession.userId, createStream, Models.Assets.Type.Mesh, Genre.All, ModerationStatus.AwaitingApproval);
        createStream.Dispose();

        return asset;
    }

    private async Task<CreateResponse> UploadMeshPart(UploadAssetRequest request, Stream stream, long creatorId, CreatorType creatorType)
    {
        byte[] meshBytes = await ReadStreamToByteArray(stream);

        var validationStream = CreateStreamFromBytes(meshBytes);
        if (!await services.assets.RobloxFileValidation(validationStream))
        {
            validationStream.Dispose();
            throw new BadRequestException(0, "Bad mesh file");
        }
        validationStream.Dispose();

        var createStream = CreateStreamFromBytes(meshBytes);
        var asset = await services.assets.CreateAsset(request.name, null, creatorId, creatorType,
            safeUserSession.userId, createStream, Models.Assets.Type.MeshPart, Genre.All, ModerationStatus.AwaitingApproval);
        createStream.Dispose();

        return asset;
    }

    private async Task<CreateResponse> UploadModel(UploadAssetRequest request, Stream stream, long creatorId, CreatorType creatorType)
    {
        byte[] modelBytes = await ReadStreamToByteArray(stream);

        var validationStream = CreateStreamFromBytes(modelBytes);
        if (!await services.assets.ValidateAssetFile(validationStream, Models.Assets.Type.Model))
        {
            validationStream.Dispose();
            throw new BadRequestException(0, "Bad model file");
        }
        validationStream.Dispose();

        var createStream = CreateStreamFromBytes(modelBytes);
        var asset = await services.assets.CreateAsset(request.name, null, creatorId, creatorType,
            safeUserSession.userId, createStream, Models.Assets.Type.Model, Genre.All, ModerationStatus.AwaitingApproval);
        createStream.Dispose();

        return asset;
    }

    private async Task<CreateResponse> UploadAnimation(UploadAssetRequest request, Stream stream, long creatorId, CreatorType creatorType)
    {
        byte[] animationBytes = await ReadStreamToByteArray(stream);

        var validationStream = CreateStreamFromBytes(animationBytes);
        if (!await services.assets.ValidateAssetFile(validationStream, Models.Assets.Type.Animation))
        {
            validationStream.Dispose();
            throw new BadRequestException(0, "Bad animation file");
        }
        validationStream.Dispose();

        var createStream = CreateStreamFromBytes(animationBytes);
        var asset = await services.assets.CreateAsset(request.name, null, creatorId, creatorType,
            safeUserSession.userId, createStream, Models.Assets.Type.Animation, Genre.All, ModerationStatus.ReviewApproved);
        createStream.Dispose();

        return asset;
    }
}
