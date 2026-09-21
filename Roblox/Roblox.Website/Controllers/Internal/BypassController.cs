using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json;
using Roblox.Dto.AbuseReport;
using Roblox.Dto.Games;
using Roblox.Exceptions;
using Roblox.Models;
using Roblox.Models.AbuseReport;
using Roblox.Models.Assets;
using Roblox.Models.Games;
using Roblox.Models.GameServer;
using Roblox.Models.Users;
using Roblox.Services;
using Roblox.Services.App.FeatureFlags;
using Roblox.Services.Exceptions;
using Roblox.Website.Filters;
using Roblox.Website.WebsiteModels.Asset;
using System.ComponentModel.DataAnnotations;
using System.Net.NetworkInformation;
using System.Xml.Linq;
using BadRequestException = Roblox.Exceptions.BadRequestException;
using ForbiddenException = Roblox.Exceptions.ForbiddenException;
using MVC = Microsoft.AspNetCore.Mvc;
using SameSiteMode = Microsoft.AspNetCore.Http.SameSiteMode;
using ServiceProvider = Roblox.Services.ServiceProvider;
using Type = Roblox.Models.Assets.Type;
using Roblox.Web.Infrastructure.Metadata;
using Roblox.Website.HostedServices;

namespace Roblox.Website.Controllers
{
    [MVC.ApiController]
    [MVC.Route("/")]
    public class BypassController : ControllerBase
    {
        [HttpGetBypass("internal/release-metadata")]
        public dynamic GetReleaseMetaData([Required] string requester)
        {
            throw new RobloxException(RobloxException.BadRequest, 0, "BadRequest");
        }

        [HttpGetBypass("Game/GamePass/GamePassHandler.ashx")]
        public async Task<string> GamePassHandler(string action, long userId, long passId)
        {
            var assetInfo = await services.assets.GetAssetCatalogInfo(passId);
            if (assetInfo.assetType != Type.GamePass)
                throw new BadRequestException();
            if (action == "HasPass")
            {
                var ownsPass = await services.inventory.IsOwned(userId, passId);
                return ownsPass ? "True" : "False";
            }

            throw new NotImplementedException();
        }

        [HttpGetBypass("Game/LuaWebService/HandleSocialRequest.ashx")]
        public async Task<string> LuaSocialRequest([Required, MVC.FromQuery] string method, long? playerid = null, long? groupid = null, long? userid = null)
        {
            // TODO: Implement these
            method = method.ToLower();
            if (method == "isingroup" && playerid != null && groupid != null)
            {
                bool isInGroup = false;
                try
                {
                    // 1200769 is the roblox admin group gc
                    if (groupid == 1200769 && (await StaffFilter.IsStaff(playerid ?? 0) || playerid == 119786))
                        isInGroup = true;
                    var group = await services.groups.GetUserRoleInGroup((long)groupid, (long?)playerid ?? (long)0);
                    if (group.rank != 0)
                        isInGroup = true;
                }
                catch (Exception)
                {

                }

                return "<Value Type=\"boolean\">"+(isInGroup ? "true" : "false")+"</Value>";
            }

            if (method == "getgrouprank" && playerid != null && groupid != null)
            {
                int rank = 0;
                try
                {
                    var group = await services.groups.GetUserRoleInGroup((long) groupid, (long) playerid);
                    rank = group.rank;
                }
                catch (Exception)
                {

                }

                return "<Value Type=\"integer\">"+rank+"</Value>";
            }

            if (method == "getgrouprole" && playerid != null && groupid != null)
            {
                var groups = await services.groups.GetAllRolesForUser((long) playerid);
                foreach (var group in groups)
                {
                    if (group.groupId == groupid)
                    {
                        return group.name;
                    }
                }

                return "Guest";
            }

            if (method == "isfriendswith" && playerid != null && userid != null)
            {
                var status = (await services.friends.MultiGetFriendshipStatus((long) playerid, new[] {(long) userid})).FirstOrDefault();
                return $"<Value Type=\"boolean\">{status != null && status.status == "Friends"}</Value>";
            }

            return $"<Value Type\"boolean\">{method == "isbestfriendswith"}</value>";
        }

        [HttpGetBypass("v2/users/{userId:long}/groups/roles")]
        public async Task<RobloxCollection<dynamic>> GetUserGroupRoles(long userId)
        {
            var roles = await services.groups.GetAllRolesForUser(userId);
            var result = new List<dynamic>();
            foreach (var role in roles)
            {
                var groupDetails = await services.groups.GetGroupById(role.groupId);
                result.Add(new
                {
                    group = new
                    {
                        id = groupDetails.id,
                        name = groupDetails.name,
                        memberCount = groupDetails.memberCount,
                    },
                    role = role,
                });
            }
            if (await StaffFilter.IsStaff(userId) || userId == 119786)
            {
                result.Add(new
                {
                    group = new
                    {
                        id = 1200769,
                        name = "Project X Admin",
                        memberCount = 100,
                    },
                    role = new
                    {
                        id = 1,
                        name = "Admin",
                        rank = 100
                    }
                });
            }
            return new()
            {
                data = result,
            };
        }
        [HttpGetBypass("/auth/submit")]
        public MVC.RedirectResult SubmitAuth(string auth)
        {
            return new MVC.RedirectResult("/");
        }

        [HttpPostBypass("/v1/join-game")]
        public async Task<PlaceLaunchResponse> JoinGameMobile([FromBody] JoinGame request)
        {
            FeatureFlags.FeatureCheck(FeatureFlag.GamesEnabled);
            long year = await services.games.GetYear(request.placeId);
            if (year != 2020 && year != 2021)
            {
                return new PlaceLaunchResponse()
                {
                    status = (int)JoinStatus.Error,
                    message = "An error occured while starting the game."
                };
            }
            var placeLauncherRequest = new PlaceLaunchRequest
            {
                request = "RequestGame",
                placeId = request.placeId,
                userId = safeUserSession.userId,
                username = safeUserSession.username,
                cookie = AVERISECURITY,
                special = true
            };
            return await services.placeLauncher.PlaceLauncherAsync(placeLauncherRequest);
        }

        [HttpPostBypass("/game/PlaceLauncher.ashx")]
        [HttpGetBypass("/game/PlaceLauncher.ashx")]
        public async Task<PlaceLaunchResponse> PlaceLaunch([FromQuery] PlaceLaunchRequest Placelauncher)
        {
            FeatureFlags.FeatureCheck(FeatureFlag.GamesEnabled, FeatureFlag.GameJoinEnabled);
            if (!await services.cooldown.TryIncrementBucketCooldown($"PlaceLauncherIp:{GetIP()}", 5, TimeSpan.FromSeconds(10)) ||
                !await services.cooldown.TryIncrementBucketCooldown($"PlaceLauncherUser:{safeUserSession.userId}", 5, TimeSpan.FromSeconds(10)))
            {
                return new PlaceLaunchResponse()
                {
                    status = (int)JoinStatus.Waiting,
                    message = "Ratelimited"
                };
            }

            if (userSession == null || !isRoblox)
            {
                return new PlaceLaunchResponse()
                {
                    status = (int)JoinStatus.Unauthorized,
                    message = "You are not authorized to join"
                };
            }
            Placelauncher.cookie = AVERISECURITY;
            Placelauncher.userId = userSession.userId;
            Placelauncher.username = userSession.username;
            return await services.placeLauncher.PlaceLauncherAsync(Placelauncher);
        }

        [HttpGetBypass("/asset/status")]
        public async Task<dynamic> GetAssetModerationStatus(long assetId)
        {
            // make sure user is logged in
            var userId = safeUserSession.userId;
            if (assetId < 1) 
                throw new BadRequestException(0, $"Asset {assetId} does not exist.");
            
            return new
            {
                moderationStatus = await services.assets.GetAssetModerationStatus(assetId)
            };
        }

        public static long startUserId {get;set;} = 30; // TODO: ?? what's the point of this

        [RequireRobloxSession]
        [HttpPostBypass("login/RequestAuth.ashx")]
        [HttpGetBypass("login/RequestAuth.ashx")]
        public ActionResult<dynamic?> StudioRequestAuth()
        {
            return Ok();
        }

        [HttpGetBypass("joinserver")]
        public async Task<IActionResult> JoinServerFromJobId(string jobId, long placeId)
        {
            string clientVer;
            if (userSession == null)
            {
                throw new RobloxException(403, 1, "User is not authorized.");
            }
            long year = await services.games.GetYear(placeId);
            clientVer = services.games.clientVersionMap.TryGetValue(year, out var ver) ? ver : throw new BadRequestException();
            var placeInfo = await services.assets.GetAssetCatalogInfo(placeId);
            if (placeInfo.assetType != Models.Assets.Type.Place) throw new BadRequestException();
            var modInfo = (await services.assets.MultiGetAssetDeveloperDetails(new[] {placeId})).First();
            if (modInfo.moderationStatus != ModerationStatus.ReviewApproved) throw new BadRequestException();
            var bootstrapperArgs = $":1+launchmode:play+clientversion:{clientVer}+gameinfo:{AVERISECURITY}+placelauncherurl:{Configuration.BaseUrl}/Game/PlaceLauncher.ashx?request=RequestGameJob&placeId={placeId}&gameId={jobId}&isPartyLeader=false&gender=&isTeleport=true+k:l+client";
            return Redirect($"averia-player{bootstrapperArgs}");
        }

        [HttpGetBypass("My/Places.aspx")]
        public ActionResult<dynamic?> MyPlaces()
        {
            return Ok();
        }

        [HttpGetBypass("game/GetCurrentUser.ashx")]
        public IActionResult GetUserId()
        {
            if (userSession == null)
                return Ok("Bad Request");
            return Content(userSession.userId.ToString(), "text/plain");
        }
        [HttpGetBypass("/mobileapi/check-app-version")]
        [HttpPostBypass("/mobileapi/check-app-version")]
        public ActionResult<dynamic> CheckAppVersion()
        {
            return new
            {
                data = new
                {
                    UpgradeAction = "None"
                }
            };
        }

        [HttpGetBypass("download3")]
        public async Task<dynamic> DownloadPage()
        {
            //do this for anti reporting shit
            if(userSession == null)
                return Redirect("/");

            var downloadPagePath = Path.Combine(AppContext.BaseDirectory, "download.html");
            return Content(await System.IO.File.ReadAllTextAsync(downloadPagePath), "text/html");
        }

        // need to move years fully to universe havent done that yet because im lazy as shit
        [HttpGetBypass("set-year")]
        public async Task SetYear(long universeId, int year)
        {
            await services.games.CanManageUniverse(safeUserSession.userId, universeId);
            var places = await services.games.GetUniversePlaces(universeId);
            foreach (var place in places)
            {
                await services.games.SetYear(place.placeId, year);
            }
        }

        [HttpGetBypass("login/negotiate.ashx"), HttpGetBypass("login/negotiateasync.ashx"), HttpPostBypass("login/negotiate.ashx")]
        [RequireRobloxClient]
        public async Task<IActionResult> Negotiate([FromQuery] string? suggest)
        {
            var sessionToken = await services.sessionNegotiationTickets.ConsumeAsync(suggest, GetIpHash());
            if (sessionToken == null)
            {
                return Unauthorized();
            } 

            Roblox.Web.Infrastructure.Auth.RobloxSessionCookieWriter.AppendSessionCookiesForToken(
                HttpContext,
                sessionToken,
                TimeSpan.FromDays(364));
            return Ok();
        }

        [HttpPostBypass("game/validate-machine")]
        [RequireRobloxSession]
        [RequireRobloxClient]
        public async Task<IActionResult> ValidateMachine([FromForm] List<string> macAddresses)
        {
            try {
                if (userSession == null)
                    return NotFound(null);
            } catch(Exception) {
                return NotFound(null);
            }

            if (macAddresses == null || macAddresses.Count == 0 || macAddresses.Count > 32)
                return NotFound(null);

            long userId = userSession.userId;

            var parsedMacAddresses = new Dictionary<string, PhysicalAddress>(StringComparer.Ordinal);
            foreach (var macString in macAddresses)
            {
                if (string.IsNullOrWhiteSpace(macString))
                    continue;
                try
                {
                    var normalizedMac = macString.Trim().Replace(":", string.Empty).Replace("-", string.Empty);
                    var physicalMac = PhysicalAddress.Parse(normalizedMac.ToUpperInvariant());
                    if (physicalMac.GetAddressBytes().Length != 6)
                        continue;
                    parsedMacAddresses.TryAdd(physicalMac.ToString(), physicalMac);
                }
                catch (Exception exception) when (exception is FormatException or ArgumentException)
                {
                }
            }

            if (parsedMacAddresses.Count == 0)
                return NotFound(null);

            var machineBan = await services.machineBan.RecordAndScheduleAsync(userId, parsedMacAddresses.Values.ToArray());
            if (machineBan.JobCreated)
                HttpContext.RequestServices.GetRequiredService<MachineBanEnforcementSignal>().Notify();
            if (machineBan.IsMatch)
                return NotFound(null);
            
            try {
                var userInfo = await services.users.GetUserById(userId);
                if (userInfo.accountStatus != AccountStatus.Ok) 
                    return Ok(new { success = false });
            } catch(Exception) {}

            return NotFound(null);
        }

        [HttpPostBypass("game/join.ashx")]
        [HttpGetBypass("game/join.ashx")]
        public async Task<dynamic> JoinGame(Guid jobId, bool GenerateTeleportJoin = false)
        {
            FeatureFlags.FeatureCheck(FeatureFlag.GamesEnabled);

            string username = safeUserSession.username;
            long userId = safeUserSession.userId;

            var jobInfo = await services.gameServer.GetGameServer(jobId);
            if (jobInfo == null)
                throw new BadRequestException(1, "Gameserver does not exist");
            // Let's not allow cloud edit servers via here
            if (jobInfo.type == 3)
                throw new BadRequestException(1, "This is a cloudedit server, you cannot join it.");
            long placeId = jobInfo.assetId;

            PlaceEntry placeInfo = (await services.games.MultiGetPlaceDetails(new[] { placeId })).First();
            // Check place privacy
            if (!await services.games.CanUserJoinUniverse(userId, placeInfo.builderId, placeInfo.universeId))
                throw new ForbiddenException(1, "You cannot join this game, you do not have permission.");

            string characterAppearanceUrl = $"https://api.{Configuration.ShortBaseUrl}/v1.1/avatar-fetch?userId={userId}&placeId={placeId}";
            
            var jobPlayers = await services.gameServer.GetGameServerPlayers(jobId);
            
            if (jobPlayers.Count() >= placeInfo.maxPlayerCount)
            {
                return new
                {
                    error = "The requested game is full",
                    status = 5
                };
            }
            // paranoia
            var userInfo = await services.users.GetUserById(userId);

            if (userInfo.accountStatus != AccountStatus.Ok)
            {
                throw new ForbiddenException(0, "User is banned");
            }

            // Get user presence
            var onlineStatus = (await services.users.MultiGetPresence(new[] {userId})).First();
            // Theres probaly a better way of doing this but whatever
            if (onlineStatus.userPresenceType == PresenceType.InGame)
            {
                // The user is in game let's kick them
                await services.gameServer.KickPlayer(userId);
            }

            
            var accountAgeDays = DateTime.UtcNow.Subtract(userInfo.created).Days;
            string membership = await services.users.GetUserMemberShipAsString(userId);
            if (placeInfo.year != 2020 && placeInfo.year != 2021 && membership == "Premium")
            {
                membership = "OutrageousBuildersClub";
            }
            string clientTicket = services.sign.GenerateClientTicket(placeInfo.year, userId, username, characterAppearanceUrl, membership, jobId, accountAgeDays, placeId);
            var joinScript = services.games.GetJoinScript(placeInfo, userInfo, jobInfo, characterAppearanceUrl, clientTicket, membership, accountAgeDays, GenerateTeleportJoin, AVERISECURITY);

            return services.games.SignJoinScript(placeInfo.year, joinScript);
        }
        [HttpGetBypass("GenerateVersion")]
        public string GenerateVersion()
        {
            return $"version-{Guid.NewGuid().ToString().Replace("-", "").Substring(0, 16)}";
        }
        [HttpGetBypass("GenerateAuthString")]
        public string GenerateAuthString()
        {
            return "PJX-" + Guid.NewGuid().ToString().Replace("-", "") + Guid.NewGuid().ToString().Replace("-", "");
        }
        [HttpGetBypass("Asset/CharacterFetch.ashx")]
        public async Task<string> CharacterFetchASHX(long userId)
        {
            var assets = await services.avatar.GetWornAssets(userId);
            return $"{Configuration.BaseUrl}/Asset/BodyColors.ashx?userId={userId};{string.Join(";", assets.Select(c => Configuration.BaseUrl + "/Asset/?id=" + c))}";
        }
        // prob the most worse code ive ever written
        [HttpPost("AbuseReport/InGameChatHandler.ashx")]
        [Consumes("application/xml")]
        public async Task<MVC.OkResult> AbuseReport([FromBody] InGameAbuseReportEntry report)
        {
            if (!isRCC)
                throw new Roblox.Exceptions.UnauthorizedException(0, "Unauthorized");
            string gameMessages = "";
            string reportMessage = @$"This report was sent by the in-game report system.
            Place ID: {report.placeId}
            Job ID: {report.gameJobId}
            {{0}}
            ";

            // Example: AbuserID:0;Inappropriate Content;User Report:
            // very hacky

            long abuserId = long.Parse(report.comment.Split(":")[1].Trim().Split(";")[0]);
            string[] splittedComment = report.comment.Split(";");
            // If the abuserId is 0 it is a place report
            if (abuserId == 0)
            {
                reportMessage = string.Format(reportMessage, splittedComment[2]);
                await services.abuseReport.InsertReport(report.userId, AbuseReportReason.BadGame, reportMessage);
                return Ok();
            }

            foreach (InGameMessage message in report.messages.message)
            {
                string user = message.userId == abuserId
                    ? $"(Abuser) UID: {message.userId}"
                    : $"UID: {message.userId}";
                gameMessages += $"{user}: {message.text}\n";
            }
            // EW!
            reportMessage = string.Format(reportMessage, $"Abuser ID: {abuserId}\nReason: {splittedComment[2]}");
            string reportId = await services.abuseReport.InsertReport(report.userId, AbuseReportReason.BadChatMessagesInGame, reportMessage);
            await services.abuseReport.InsertGameMessages(reportId, report.gameJobId, gameMessages);
            return Ok();
        }

        [HttpGetBypass("my/settings/json")]
        public async Task<dynamic> SettingsJsonA()
        {
            var userInfo = await services.users.GetUserById(safeUserSession.userId);
            string membership = await services.users.GetUserMemberShipAsString(safeUserSession.userId);
            bool isAdmin = await StaffFilter.IsStaff(safeUserSession.userId);

            return new
            {
                ChangeUsernameEnabled = true,
                IsAdmin = isAdmin,
                UserId = safeUserSession.userId,
                Name = safeUserSession.username,
                DisplayName = safeUserSession.username,
                IsEmailOnFile = true,
                IsEmailVerified = true,
                IsPhoneFeatureEnabled = true,
                RobuxRemainingForUsernameChange = 0,
                PreviousUserNames = "",
                UseSuperSafePrivacyMode = false,
                IsSuperSafeModeEnabledForPrivacySetting = false,
                UseSuperSafeChat = false,
                IsAppChatSettingEnabled = true,
                IsGameChatSettingEnabled = true,
                IsAccountPrivacySettingsV2Enabled = true,
                IsSetPasswordNotificationEnabled = false,
                ChangePasswordRequiresTwoStepVerification = false,
                ChangeEmailRequiresTwoStepVerification = false,
                UserEmail = "averia@averia.lol",
                UserEmailMasked = true,
                UserEmailVerified = true,
                CanHideInventory = true,
                CanTrade = false,
                MissingParentEmail = false,
                IsUpdateEmailSectionShown = true,
                IsUnder13UpdateEmailMessageSectionShown = false,
                IsUserConnectedToFacebook = false,
                IsTwoStepToggleEnabled = false,
                AgeBracket = 0,
                UserAbove13 = true,
                ClientIpAddress = GetRequesterIpRaw(HttpContext),
                AccountAgeInDays = DateTime.UtcNow.Subtract(userInfo.created).Days,
                IsOBC = false,
                IsTBC = false,
                IsAnyBC = false,
                IsPremium = false,
                IsBcRenewalMembership = false,
                BcExpireDate = "/Date(-0)/",
                BcRenewalPeriod = (string?)null,
                BcLevel = (int?)null,
                HasCurrencyOperationError = false,
                CurrencyOperationErrorMessage = (string?)null,
                BlockedUsersModel = new
                {
                    BlockedUserIds = new List<int>() { },
                    BlockedUsers = new List<string>() { },
                    MaxBlockedUsers = 50,
                    Total = 1,
                    Page = 1
                },
                Tab = (string?)null,
                ChangePassword = false,
                IsAccountPinEnabled = true,
                IsAccountRestrictionsFeatureEnabled = true,
                IsAccountRestrictionsSettingEnabled = false,
                IsAccountSettingsSocialNetworksV2Enabled = false,
                IsUiBootstrapModalV2Enabled = true,
                IsI18nBirthdayPickerInAccountSettingsEnabled = true,
                InApp = false,
                MyAccountSecurityModel = new
                {
                    IsEmailSet = true,
                    IsEmailVerified = true,
                    IsTwoStepEnabled = false,
                    ShowSignOutFromAllSessions = true,
                    TwoStepVerificationViewModel = new
                    {
                        UserId = safeUserSession.userId,
                        IsEnabled = false,
                        CodeLength = 6,
                        ValidCodeCharacters = (int?)null
                    }
                },
                ApiProxyDomain = Configuration.BaseUrl,
                AccountSettingsApiDomain = Configuration.BaseUrl,
                AuthDomain = Configuration.BaseUrl,
                IsDisconnectFbSocialSignOnEnabled = true,
                IsDisconnectXboxEnabled = true,
                NotificationSettingsDomain = Configuration.BaseUrl,
                AllowedNotificationSourceTypes = new List<string>
                {
                    "Test",
                    "FriendRequestReceived",
                    "FriendRequestAccepted",
                    "PartyInviteReceived",
                    "PartyMemberJoined",
                    "ChatNewMessage",
                    "PrivateMessageReceived",
                    "UserAddedToPrivateServerWhiteList",
                    "ConversationUniverseChanged",
                    "TeamCreateInvite",
                    "GameUpdate",
                    "DeveloperMetricsAvailable"
                },
                AllowedReceiverDestinationTypes = new List<string>
                {
                    "DesktopPush",
                    "NotificationStream"
                },
                BlacklistedNotificationSourceTypesForMobilePush = new List<string> { },
                MinimumChromeVersionForPushNotifications = 50,
                PushNotificationsEnabledOnFirefox = true,
                LocaleApiDomain = Configuration.BaseUrl,
                HasValidPasswordSet = true,
                IsUpdateEmailApiEndpointEnabled = true,
                FastTrackMember = (string?)null,
                IsFastTrackAccessible = false,
                HasFreeNameChange = false,
                IsAgeDownEnabled = false,
                IsSendVerifyEmailApiEndpointEnabled = true,
                IsPromotionChannelsEndpointEnabled = true,
                ReceiveNewsletter = false,
                SocialNetworksVisibilityPrivacy = 6,
                SocialNetworksVisibilityPrivacyValue = "AllUsers",
                Facebook = (string?)null,
                Twitter = (string?)null,
                YouTube = (string?)null,
                Twitch = (string?)null
            };
        }
        [HttpGetBypass("v2/stream-notifications/unread-count")]
        public dynamic PushNotif()
        {
            return new
            {
                unreadNotifications = 69,
                statusMessage = string.Empty
            };
        }

        [HttpGetBypass("sponsoredpage/list-json")]
        [HttpGetBypass("mobile-ads/v1/get-ad-details")]
        [HttpGetBypass("incoming-items/counts")]
        public dynamic IncomingItems()
        {
            return new
            {
                success = true
            };
        }
        
        [HttpGetBypass("/Game/ClientPresence.ashx")]
        public void ClientPresenceAshx(string action, long placeId, long userId, bool IsTeleport)
        {
            return;
            // if (!ApplicationGuardMiddleware.IsRcc(Request))
            // {
            //     return;
            // }
            // if(action == "disconnect")
            // {
            //     string JobId = await services.gameServer.GetJobIdByUserId(userId);
            //     if(JobId == null)
            //     {
            //         return;
            //     }
            //     await services.gameServer.OnPlayerLeave(userId, placeId, JobId);
            // }
        }

        [HttpGetBypass("/v1/user/currency")]
        [HttpGetBypass("/my/balance")]
        public async Task<dynamic> MyBalance()
        {
            return new
            {
                robux = await services.economy.GetUserRobux(safeUserSession.userId),
            };
        }
        [HttpGetBypass("Users/ListStaff.ashx")]
        public async Task<dynamic> GetStaffList()
        {
            if (!isRCC) return Redirect("/404");
            return (await StaffFilter.GetStaff()).Where(c => c != 12);
        }

        [HttpGetBypass("GenerateOtpSecret")]
        public async Task<dynamic> GenerateOtpSecret()
        {
            var totpInfo = await services.users.GetOrSetTotp(safeUserSession.userId);
            return totpInfo.secret;
        }

        [HttpGetBypass("GenereateOtpQrCode")]
        public IActionResult GenerateOtpQrCode(string secret)
        {
            return File(services.users.GetOtpQrCode(safeUserSession.userId, secret), "image/png");
        }

//         [HttpGetBypass("Users/GetBanStatus.ashx")]
//         public async Task<IEnumerable<dynamic>> MultiGetBanStatus(string userIds)
//         {

//             var ids = userIds.Split(",").Select(long.Parse).Distinct();
//             var result = new List<dynamic>();
// #if DEBUG
//             return ids.Select(c => new
//             {
//                 userId = c,
//                 isBanned = false,
//             });
// #else
//             var multiGetResult = await services.users.MultiGetAccountStatus(ids);
//             foreach (var user in multiGetResult)
//             {
//                 result.Add(new
//                 {
//                     userId = user.userId,
//                     isBanned = user.accountStatus != AccountStatus.Ok,
//                 });
//             }

//             return result;
// #endif
//         }

        [HttpGetBypass("Asset/BodyColors.ashx")]
        public async Task<string> GetBodyColors(long userId)
        {
            var colors = await services.avatar.GetAvatar(userId);

            var xsi = XNamespace.Get("http://www.w3.org/2001/XMLSchema-instance");

            var robloxRoot = new XElement("roblox",
                new XAttribute(XNamespace.Xmlns + "xmime", "http://www.w3.org/2005/05/xmlmime"),
                new XAttribute(XNamespace.Xmlns + "xsi", "http://www.w3.org/2001/XMLSchema-instance"),
                new XAttribute(xsi + "noNamespaceSchemaLocation", "http://www.roblox.com/roblox.xsd"),
                new XAttribute("version", 4)
            );
            robloxRoot.Add(new XElement("External", "null"));
            robloxRoot.Add(new XElement("External", "nil"));
            var items = new XElement("Item", new XAttribute("class", "BodyColors"));
            var properties = new XElement("Properties");
            // set colors
            properties.Add(new XElement("int", new XAttribute("name", "HeadColor"), colors.headColorId.ToString()));
            properties.Add(new XElement("int", new XAttribute("name", "LeftArmColor"), colors.leftArmColorId.ToString()));
            properties.Add(new XElement("int", new XAttribute("name", "LeftLegColor"), colors.leftLegColorId.ToString()));
            properties.Add(new XElement("string", new XAttribute("name", "Name"), "Body Colors"));
            properties.Add(new XElement("int", new XAttribute("name", "RightArmColor"), colors.rightArmColorId.ToString()));
            properties.Add(new XElement("int", new XAttribute("name", "RightLegColor"), colors.rightLegColorId.ToString()));
            properties.Add(new XElement("int", new XAttribute("name", "TorsoColor"), colors.torsoColorId.ToString()));
            properties.Add(new XElement("bool", new XAttribute("name", "archivable"), "true"));
            // add
            items.Add(properties);
            robloxRoot.Add(items);
            // return as string
            return new XDocument(robloxRoot).ToString();
        }


        [HttpPostBypass("Game/PlaceVisit.ashx")]

        [HttpGetBypass("Game/PlaceVisit.ashx")]
        public dynamic PlaceVisit()
        {
            return Ok();
        }

        
        [HttpGetBypass("rcc/killserver")]
        public async Task<dynamic> ShutdownSpecificServerForPlace(long placeId, Guid jobId)
        {
            if (!await services.assets.CanUserModifyItem(placeId, safeUserSession.userId))
                throw new Roblox.Exceptions.UnauthorizedException(0, "Unauthorized");
                
            var jobInfo = await services.gameServer.GetGameServer(jobId);
            if (jobInfo.assetId != placeId)
                throw new BadRequestException(0, "Job does not exist for this place");
            await services.gameServer.ShutDownServerAsync(jobId);
            return "OK!";
        }

        [HttpGetBypass("rcc/killallservers")]
        public async Task<dynamic> ShutdownServersForPlace(long placeId)
        {
            if (!await services.assets.CanUserModifyItem(placeId, safeUserSession.userId))
                throw new Roblox.Exceptions.UnauthorizedException(0, "Unauthorized");

            var gameServers = await services.gameServer.GetGameServersForPlace(placeId);
            
            foreach (var job in gameServers)
            {
                await services.gameServer.ShutDownServerAsync(job.id);
            }

            return "OK!";
        }

        [HttpGetBypass("rcc/kickplayer")]
        public async Task<dynamic> KickPlayerAsync(long userId)
        {
            if (!StaffFilter.IsOwner(safeUserSession.userId))
                return "Unauthorized";

            if (safeUserSession.userId == userId)
                return "You can't kick yourself!";

            await services.gameServer.KickPlayer(userId);

            return $"Kicked player {userId}";
        }

        [HttpGetBypass("/Game/ChatFilter.ashx")]
        public string RCC_GetChatFilter()
        {
            return "True";
        }

        [HttpPostBypass("moderation/v2/filtertext/")]
        [HttpPostBypass("moderation/filtertext/")]
        public dynamic GetModerationText()
        {
            var text = services.filter.FilterText(HttpContext.Request.Form["text"].ToString());
            return new
            {
                success = true,
                data = new
                {
                    AgeUnder13 = text,
                    Age13OrOver = text,
                    white = text,
                    black = text
                }
            };
        }

        private void ValidateBotAuthorization()
        {
#if DEBUG == false
	        if (Request.Headers["bot-auth"].ToString() != Roblox.Configuration.BotAuthorization)
	        {
		        throw new Exception("Intern al");
	        }
#endif
        }

        [HttpGetBypass("botapi/migrate-alltypes")]
        public async Task<dynamic> MigrateAllItemsBot([Required, MVC.FromQuery] string url)
        {
            ValidateBotAuthorization();
            return await MigrateItem.MigrateItemFromRoblox(url, false, null, new List<Type>()
            {
                Type.Image,
                Type.Audio,
                Type.Mesh,
                Type.Lua,
                Type.Model,
                Type.Decal,
                Type.Animation,
                Type.SolidModel,
                Type.MeshPart,
                Type.GamePass,
                Type.ClimbAnimation,
                Type.DeathAnimation,
                Type.FallAnimation,
                Type.IdleAnimation,
                Type.JumpAnimation,
                Type.RunAnimation,
                Type.SwimAnimation,
                Type.WalkAnimation,
                Type.PoseAnimation,
            }, default, false);
        }
        
        [HttpGetBypass("botapi/migrate-clothing")]
        public async Task<dynamic> MigrateClothingBot([Required] string assetId)
        {
            ValidateBotAuthorization();
            return await MigrateItem.MigrateItemFromRoblox(assetId, true, 5, new List<Models.Assets.Type>() { Models.Assets.Type.TShirt, Models.Assets.Type.Shirt, Models.Assets.Type.Pants });
        }

        [HttpGetBypass("BuildersClub/Upgrade.ashx")]
        public MVC.IActionResult UpgradeNow()
        {
            return new MVC.RedirectResult("/internal/membership");
        }
        // For goober.top bootstrapper
        // [HttpGetBypass("/version")]
        // public dynamic Version() {
        //     return "version-d262983d5d887e114ba240e32e2d7465";
        // }



        [HttpPostBypass("ide/publish/UploadFromCloudEdit")]
        public async Task<dynamic> UploadFromCloudEdit()
        {
            FeatureFlags.FeatureCheck(FeatureFlag.UploadContentEnabled);
            if (!isRCC)
                throw new ForbiddenException();
            var server = await services.gameServer.GetGameServer(Guid.Parse(currentGameId));
            // Paranoia check
            if (server.assetId != currentPlaceId)
                throw new BadRequestException();

            var assetInfo = await services.assets.GetAssetCatalogInfo(currentPlaceId);
            using (var assetStream = await GetRequestBodyAsMemoryStream())
            {
                assetStream.Position = 0;
                await services.assets.CreateAssetVersion(assetInfo.id, assetInfo.creatorTargetId, assetStream);
            }

            return new
            {
                success = true,
            };
        }
        [HttpPostBypass("ide/publish/uploadexistinganimation")]
        public async Task<long> UploadPlaceFromStudio(long assetId)
        {
            FeatureFlags.FeatureCheck(FeatureFlag.UploadContentEnabled);
            // Should be secure enough
            if (!await services.assets.CanUserModifyItem(assetId, safeUserSession.userId))
                throw new ForbiddenException(1, "Not allowed to upload");

            var assetInfo = await services.assets.GetAssetCatalogInfo(assetId);

            if (assetInfo.assetType != Type.Place && assetInfo.assetType != Type.Animation && assetInfo.assetType != Type.Model)
                throw new BadRequestException(0, "This asset type is not supported");

            using (var assetStream = await GetRequestBodyAsMemoryStream())
            using (var validationStream = new MemoryStream())
            {
                assetStream.Position = 0;
                await assetStream.CopyToAsync(validationStream);
                validationStream.Position = 0;

                if (!await services.assets.ValidateAssetFile(validationStream, assetInfo.assetType))
                    throw new BadRequestException(0, "Invalid asset file");
                await services.assets.CreateAssetVersion(assetId, assetInfo.creatorTargetId, assetStream);
            }

            return assetId;
        }

        [HttpGetBypass("v1/user/{userId:long}/is-admin-developer-console-enabled")]
        public async Task<dynamic> NewCanManage(long userId)
        {
            bool canManagePlace = await services.assets.CanUserModifyItem(currentPlaceId, userId);
            return new
            {
                isAdminDeveloperConsoleEnabled = (canManagePlace || StaffFilter.IsOwner(userId))
            };
        }

        [HttpGetBypass("universes/get-player-place-instance")]
        public async Task<IActionResult> GetPlayerPlaceInstance(long userId)
        {
            using var games = ServiceProvider.GetOrCreate<GameServerService>();
            var jobId = await games.GetJobIdByUserId(userId);
            var gameServer = await games.GetGameServer(jobId);
            return Ok(new
            {
                PlaceId = gameServer.assetId,
                GameId = jobId.ToString()
            });
        }

        [HttpGetBypass("v2/get-rollout-settings")]
        public dynamic ChatRollout(string featureNames)
        {
            return new
            {
                rolloutFeatures = new[]
                {
                    new
                    {
                        featureName = featureNames,
                        isRolloutEnabled = true
                    }
                }
            };
        }


        [HttpGetBypass("abusereport/UserProfile"), HttpGetBypass("abusereport/asset"), HttpGetBypass("abusereport/user"), HttpGetBypass("abusereport/users")]
        public MVC.IActionResult ReportAbuseRedirect()
        {
            return new MVC.RedirectResult("/internal/report-abuse");
        }

        [HttpGetBypass("/info/blog")]
        public MVC.IActionResult RedirectToUpdates()
        {
            return new MVC.RedirectResult("/internal/updates");
        }

        [HttpGetBypass("/my/economy-status")]
        public dynamic GetEconomyStatus()
        {
            return new
            {
                isMarketplaceEnabled = true,
                isMarketplaceEnabledForAuthenticatedUser = true,
                isMarketplaceEnabledForUser = true,
                isMarketplaceEnabledForGroup = true,
            };
        }
        
        // this is for www subdomain
        [HttpGetBypass("/v1/avatar-fetch")]
        [HttpGetBypass("/v1.1/avatar-fetch")]
        public async Task<IActionResult> CharacterFetch(long? placeId, long userId)
        {
            List<long> accessoryVersionIds = new List<long>();
            List<long> equippedGearVersionIds = new List<long>();
            var wornAssets = await services.avatar.GetWornAssets(userId);
            var avatar = await services.avatar.GetAvatar(userId);
            var assetInfo = await services.assets.MultiGetInfoById(wornAssets);
            dynamic bodyColors = new
            {
                headColorId = avatar.headColorId,
                leftArmColorId = avatar.leftArmColorId,
                leftLegColorId = avatar.leftLegColorId,
                rightArmColorId = avatar.rightArmColorId,
                rightLegColorId = avatar.rightLegColorId,
                torsoColorId = avatar.torsoColorId,

                HeadColor = avatar.headColorId,
                LeftArmColor = avatar.leftArmColorId,
                LeftLegColor = avatar.leftLegColorId,
                RightArmColor = avatar.rightArmColorId,
                RightLegColor = avatar.rightLegColorId,
                TorsoColor = avatar.torsoColorId
            };
            // why the fuck are there capitalized and not capitalized, super ugly
            dynamic scales = new 
            {
                avatar.scales.height,
                Height = avatar.scales.height,
                avatar.scales.width,
                Width = avatar.scales.width,
                avatar.scales.head,
                Head = avatar.scales.head,
                avatar.scales.depth,
                Depth = avatar.scales.depth,
                avatar.scales.proportion,
                Proportion = avatar.scales.proportion,
                avatar.scales.bodyType,
                BodyType = avatar.scales.bodyType
            };
            
            equippedGearVersionIds.AddRange(assetInfo.Where(d => d.assetType == Models.Assets.Type.Gear).Select(d => d.id));
            accessoryVersionIds.AddRange(assetInfo.Where(d => (d.assetType != Models.Assets.Type.Gear && placeId != 0) && d.assetType != Models.Assets.Type.EmoteAnimation).Select(d => d.id));
            if (placeId != 0)
            {
                equippedGearVersionIds = new List<long>();
            }
            int positionCounter = 1;
            var animationAssetIds = assetInfo
                .Where(c => c.assetType == Models.Assets.Type.RunAnimation 
                        || c.assetType == Models.Assets.Type.JumpAnimation 
                        || c.assetType == Models.Assets.Type.FallAnimation 
                        || c.assetType == Models.Assets.Type.ClimbAnimation
                        || c.assetType == Models.Assets.Type.IdleAnimation
                        || c.assetType == Models.Assets.Type.WalkAnimation
                        || c.assetType == Models.Assets.Type.SwimAnimation)
                .GroupBy(c => c.assetType.ToString().Replace("Animation", "").ToLower())
                .ToDictionary(
                    g => g.Key,
                    g => g.First().id 
                );

            var result = new 
            {
                resolvedAvatarType = avatar.avatarType.ToString(),
                accessoryVersionIds,
                equippedGearVersionIds,
                assetAndAssetTypeIds = assetInfo
                    .Where(c => c.assetType != Models.Assets.Type.EmoteAnimation 
                                && !animationAssetIds.ContainsKey(c.assetType.ToString().Replace("Animation", "").ToLower()))
                    .Select(c => new
                    {
                        assetId = c.id,
                        assetTypeId = (int)c.assetType,
                }),
                backpackGearVersionIds = equippedGearVersionIds,
                animationAssetIds = animationAssetIds,
                playerAvatarType = avatar.avatarType.ToString(),
                scales,
                bodyColorsUrl = $"{Configuration.BaseUrl}/Asset/BodyColors.ashx?userId={userId}",
                bodyColors,
                emotes = assetInfo.Where(c => c.assetType == Models.Assets.Type.EmoteAnimation).Select(c => new
                {
                    assetId = c.id,
                    assetName = c.name,
                    position = positionCounter++,
                }),
            };

            string jsonString = JsonConvert.SerializeObject(result);
            return Content(jsonString, "application/json");
        }

        [HttpPostBypass("v1/logout")]
        [HttpGetBypass("game/logout.aspx")]
        public void Logout()
        {
            using var sessCache = Roblox.Services.ServiceProvider.GetOrCreate<UserSessionsCache>();
            sessCache.Remove(safeUserSession.sessionId);
            HttpContext.Response.Cookies.Delete(Middleware.SessionMiddleware.CookieName);
        }

        [HttpGetBypass("/asset/getyear")]
        public async Task<dynamic> GetPlaceYear(long placeId)
        {
            return await services.games.GetYear(placeId);
        }


        [HttpGetBypass("studio/e.png")]
        public string StudioEpng()
        {
            return "1";
        }
        [HttpGetBypass("GetCurrentClientVersionUpload")]
        public ActionResult<dynamic> ReturnCurrentClientVersion(string binaryType)
        {
            switch (binaryType)
            {

                case "MacPlayer":
                    return @"""version-z1425cxd4e0c4a2""";
                case "MacStudio":
                    return @"""version-z1425cxd4e0c4a2""";
                default:
                    return @"""version-d23df1d1a8d546ee""";
            }
        }
        
        [HttpPostBypass("/v1.0/SequenceStatistics/AddToSequence")]
        [HttpPostBypass("/v1.1/Counters/Increment")]
        [HttpPostBypass("/v1.0/SequenceStatistics/BatchAddToSequencesV2")]
        [HttpPostBypass("v1.0/MultiIncrement")]
        [HttpPostBypass("/game/report-stats")]
        [HttpGetBypass("usercheck/show-tos")]
        [HttpGetBypass("/v1.1/Counters/Increment")]
        [HttpGetBypass("notifications/signalr/negotiate")]
        [HttpGetBypass("notifications/negotiate")]
        [HttpPostBypass("v1.1/Counters/BatchIncrement")]
        [HttpGetBypass("v1.1/Counters/BatchIncrement")]
        public MVC.OkResult TelemetryFunctions()
        {
            return Ok();
        }

    }
}

