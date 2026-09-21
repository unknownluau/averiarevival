using System.Diagnostics;
using System.Text;
using System.Net;
using System.Net.Sockets;
using System.Text.Json;
using Dapper;
using Roblox.Dto.Games;
using Roblox.Libraries.EasyJwt;
using Roblox.Libraries.Password;
using Roblox.Models.Assets;
using Roblox.Models.Economy;
using Roblox.Models.GameServer;
using Roblox.Services.Exceptions;
using Roblox.Logging;
using System.Collections.Concurrent;
using Roblox.Dto.Users;

namespace Roblox.Services;



public class GameServerService : ServiceBase
{
    private static readonly TimeSpan EmptyServerTtl = TimeSpan.FromMinutes(10);
    private const int EmptyServerCleanupBatchSize = 100;

    public class ArbiterHttpClient : HttpClient
    {

  
        private static readonly JsonSerializerOptions JsonOptions = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
        };

        public ArbiterHttpClient()
        {
            this.BaseAddress = new Uri($"https://arbiter.{Configuration.ShortBaseUrl}/");
            this.DefaultRequestHeaders.Add("rblx-authorization", Configuration.ArbiterAuthorization);
        }

        private async Task<HttpResponseMessage> PostLimitedAsync(string url, HttpContent content)
        {
            return await base.PostAsync(url, content);
        }

        public async Task<StartGameServerResponse?> StartGameServer(StartGameServerRequest request)
        {
            var content = new StringContent(JsonSerializer.Serialize(request), Encoding.UTF8, "application/json");
            var result = await PostLimitedAsync("start-game-server", content);
            if (!result.IsSuccessStatusCode)
                return null;

            await using var response = await result.Content.ReadAsStreamAsync();
            return await JsonSerializer.DeserializeAsync<StartGameServerResponse>(response, JsonOptions);
        }

        public async Task<bool> EvictPlayer(EvictPlayerRequest request)
        {
            var content = new StringContent(JsonSerializer.Serialize(request), Encoding.UTF8, "application/json");

            var result = await PostLimitedAsync("evict-player", content);
            return result.IsSuccessStatusCode;
        }

        public async Task<bool> KillGameServer(KillGameServerRequest request)
        {
            var content = new StringContent(JsonSerializer.Serialize(request), Encoding.UTF8, "application/json");
            var result = await PostLimitedAsync("kill-game-server", content);
            var json = await result.Content.ReadAsStringAsync();
            var parsed = JsonSerializer.Deserialize<dynamic>(json, JsonOptions);
            return parsed?.Success ?? false;
        }

        public async Task<bool> SetFilteringEnabled(SetFilteringEnabledRequest request)
        {
            var content = new StringContent(JsonSerializer.Serialize(request), Encoding.UTF8, "application/json");
            var result = await PostLimitedAsync("set-filtering-enabled", content);
            return result.IsSuccessStatusCode;
        }
        public static SetFilteringEnabledRequest CreateFilteringEnabled(Guid jobId, bool isEnabled)
        {
            return new SetFilteringEnabledRequest
            {
                jobId = jobId,
                isEnabled = isEnabled
            };
        }
        public static EvictPlayerRequest CreateEvictPlayerRequest(Guid jobId, long userId)
        {
            return new EvictPlayerRequest
            {
                gameId = jobId,
                userId = userId,
                messageVersionId = 0
            };
        }
        public static StartGameServerRequest CreateGameServerRequest(PlaceEntry placeInfo, Guid jobId, int matchmaking)
        {
            return new StartGameServerRequest
            {
                jobId = jobId,
                placeId = placeInfo.placeId,
                universeId = placeInfo.universeId,
                maxPlayerCount = placeInfo.maxPlayerCount,
                creatorId = placeInfo.builderId,
                placeVersion = 1,
                matchmakingContextId = matchmaking,
                year = placeInfo.year,
            };
        }
        public static KillGameServerRequest CreateKillGameServerRequest(Guid jobId)
        {
            return new KillGameServerRequest
            {
                jobId = jobId,
            };
        }
        public class SetFilteringEnabledRequest
        {
            public Guid jobId { get; set; }
            public bool isEnabled { get; set; }
        }
        public class EvictPlayerRequest
        {
            public Guid gameId { get; set; }
            public long userId { get; set; }
            public int messageVersionId { get; set; }
        }
        public class StartGameServerRequest
        {
            public Guid jobId { get; set; }
            public long placeId { get; set; }
            public long universeId { get; set; }
            public int maxPlayerCount { get; set; }
            public long creatorId { get; set; }
            public long placeVersion { get; set; }
            public int matchmakingContextId { get; set; }
            public long year { get; set; }
        }

        public class StartGameServerResponse
        {
            public string status { get; set; }
            public Guid jobId { get; set; }
            public int rccPort { get; set; }
            public int gameServerPort { get; set; }
            public int proxyPort { get; set; }
            public int? rccProcessId { get; set; }
            public int? quilkinProcessId { get; set; }
        }

        public class KillGameServerRequest
        {
            public Guid jobId { get; set; }
        }
    }

    private static ArbiterHttpClient arbiterClient = new ArbiterHttpClient();
    private static string jwtKey { get; set; } = string.Empty;
    private static EasyJwt jwt { get; } = new();

    public static ConcurrentDictionary<long, long> currentPlayersInGame = new ConcurrentDictionary<long, long>() { }; // userid, placeid
    public static void Configure(string newJwtKey)
    {
        jwtKey = newJwtKey;
    }

    public async Task OnPlayerJoin(long userId, long placeId, Guid serverId)
    {
        currentPlayersInGame.AddOrUpdate(userId, placeId, (key, oldValue) => placeId);
        await db.ExecuteAsync(
            "INSERT INTO asset_server_player (asset_id, user_id, server_id) VALUES (:asset_id, :user_id, :server_id::uuid)",
            new
            {
                asset_id = placeId,
                user_id = userId,
                server_id = serverId,
            });
        await InsertAsync("asset_play_history", new
        {
            asset_id = placeId,
            user_id = userId,
        });
        await db.ExecuteAsync("UPDATE asset_place SET visit_count = visit_count + 1 WHERE asset_id = :id", new
        {
            id = placeId,
        });

        // give ticket to creator
        await InTransaction(async _ =>
        {
            using var assets = ServiceProvider.GetOrCreate<AssetsService>(this);
            var placeDetails = await assets.GetAssetCatalogInfo(placeId);
            using var ec = ServiceProvider.GetOrCreate<EconomyService>(this);
            using var cooldown = ServiceProvider.GetOrCreate<CooldownService>(this);
            // Per 100 users there is a 1 day cooldown to earn tickets from visits
            if (await cooldown.TryIncrementBucketCooldown("TicketCreatorPlaceVisit:" + placeId, 100, TimeSpan.FromDays(1)))
            {
                if (placeDetails.creatorType == CreatorType.User)
                {
                    /* 
                        Homestead = 6
                        Bricksmith = 7
                    */
                    using var accountService = ServiceProvider.GetOrCreate<AccountInformationService>(this);
                    var badges = await accountService.GetUserBadges(placeDetails.creatorTargetId);
                    using var games = ServiceProvider.GetOrCreate<GamesService>(this);
                    switch (await games.GetTotalVisitsFromUser(placeDetails.creatorTargetId))
                    {
                        case 100:
                            if (badges.Any(b => b.id == 6))
                            {
                                return 0;
                            }
                            await db.ExecuteAsync("INSERT INTO user_badge (user_id, badge_id) VALUES (:user_id, :badge_id)", new
                            {
                                user_id = placeDetails.creatorTargetId,
                                badge_id = 6,
                            });
                            break;
                        case 1000:
                            if (badges.Any(b => b.id == 7))
                            {
                                return 0;
                            }
                            await db.ExecuteAsync("INSERT INTO user_badge (user_id, badge_id) VALUES (:user_id, :badge_id)", new
                            {
                                user_id = placeDetails.creatorTargetId,
                                badge_id = 7,
                            });
                            break;
                        default:
                            break;
                    }
                }
            }


            return 0;
        });
    }

    public async Task OnPlayerLeave(long userId, long placeId, Guid serverId)
    {
        currentPlayersInGame.TryRemove(userId, out _);

        await db.ExecuteAsync(
            "DELETE FROM asset_server_player WHERE user_id = :user_id AND server_id = :server_id::uuid", new
            {
                server_id = serverId,
                user_id = userId,
            });
        Console.WriteLine("deleted from db line 195 onplayerleave");
        var latestSession = await db.QuerySingleOrDefaultAsync<AssetPlayEntry>(
            "SELECT id, created_at as createdAt FROM asset_play_history WHERE user_id = :user_id AND asset_id = :asset_id AND ended_at IS NULL ORDER BY asset_play_history.id DESC LIMIT 1",
            new
            {
                user_id = userId,
                asset_id = placeId,
            });
        if (latestSession != null)
        {
            await db.ExecuteAsync("UPDATE asset_play_history SET ended_at = now() WHERE id = :id", new
            {
                id = latestSession.id,
            });

            if (latestSession.createdAt.Year != DateTime.UtcNow.Year) return;

            var playTimeMinutes = (long)Math.Truncate((DateTime.UtcNow - latestSession.createdAt).TotalMinutes);
            var earnedTickets = Math.Min(playTimeMinutes * 10, 60); // temp cap, might reduce in the future?
            // cap is 10k tickets per 12 hours (about 1k robux)
            const long maxEarningsPerPeriod = 10000;
            using (var ec = ServiceProvider.GetOrCreate<EconomyService>(this))
            {
                var earningsToday =
                    await ec.CountTransactionEarningsOfType(userId, PurchaseType.PlayingGame, null, TimeSpan.FromHours(12));

                if (earningsToday >= maxEarningsPerPeriod)
                    return;
            }

            await InTransaction(async _ =>
            {
                using var ec = ServiceProvider.GetOrCreate<EconomyService>(this);
                await ec.IncrementCurrency(CreatorType.User, userId, CurrencyType.Tickets, earnedTickets);
                await InsertAsync("user_transaction", new
                {
                    amount = earnedTickets,
                    currency_type = CurrencyType.Tickets,
                    user_id_one = userId,
                    user_id_two = 1,
                    type = PurchaseType.PlayingGame,
                    // store id of the game they played as well
                    asset_id = placeId,
                });

                return 0;
            });
        }
    }
    public async Task KickPlayer(long userId)
    {
        Guid jobId = await GetJobIdByUserId(userId);
        await arbiterClient.EvictPlayer(ArbiterHttpClient.CreateEvictPlayerRequest(jobId, userId));
    }
    public async Task KickPlayer(long userId, Guid jobId)
    {
        await arbiterClient.EvictPlayer(ArbiterHttpClient.CreateEvictPlayerRequest(jobId, userId));
    }

    public async Task ShutDownServerAsync(Guid serverId)
    {
        try
        {
            await TryDeleteGameServer(serverId);

            var killed = await arbiterClient.KillGameServer(ArbiterHttpClient.CreateKillGameServerRequest(serverId));
            if (!killed)
                Console.Error.WriteLine($"Arbiter rejected shutdown request for server {serverId}");
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Error shutting down server {serverId}: {ex}");
        }
    }


    public static void RemoveAllPlayersFromPlaceId(long placeId)
    {
        List<long> playersToRemove = currentPlayersInGame.Where(kvp => kvp.Value == placeId).Select(kvp => kvp.Key).ToList();

        foreach (var userId in playersToRemove)
        {
            currentPlayersInGame.TryRemove(userId, out _);
        }
    }

    public static long GetUserPlaceId(long userId) // get user game is in
    {
        bool isInGame = currentPlayersInGame.ContainsKey(userId);
        if (!isInGame)
            return 0;

        return currentPlayersInGame[userId];
    }

    public async Task<DateTime> GetLastServerPing(string serverId)
    {
        var result = await db.QuerySingleOrDefaultAsync<DateTime>("SELECT updated_at FROM asset_server WHERE id = :id::uuid", new
        {
            id = serverId,
        });

        return result;
    }
    public async Task<long> GetServerStat(Guid serverId)
    {
        var result = await db.QuerySingleOrDefaultAsync<long>("SELECT ping FROM asset_server WHERE id = :id::uuid", new
        {
            id = serverId,
        });

        if (result == 0)
            return -1;

        return result;
    }

    public async Task SetServerStats(string serverId, long ping, long fps)
    {
        await db.ExecuteAsync("UPDATE asset_server SET ping = :ping, fps = :fps WHERE id = :id::uuid", new
        {
            ping,
            fps,
            id = serverId,
        });
    }
    public async Task SetServerPing(Guid serverId)
    {
        await db.ExecuteAsync("UPDATE asset_server SET updated_at = :u, status = :stat WHERE id = :id::uuid", new
        {
            u = DateTime.UtcNow,
            stat = ServerStatus.Ready,
            id = serverId,
        });
    }

    public async Task DeleteGameServer(Guid serverId)
    {
        await TryDeleteGameServer(serverId);
    }

    public async Task<int> DeleteOldGameServers()
    {
        var cutoff = DateTime.UtcNow.AddMinutes(-5);

        var serverIds = (await db.QueryAsync<Guid>(
            @"SELECT s.id
          FROM asset_server s
          WHERE s.updated_at < :cutoff
            AND NOT EXISTS (
                SELECT 1
                FROM asset_server_player p
                WHERE p.server_id = s.id
            )
          ORDER BY s.updated_at
          LIMIT :limit",
            new
            {
                cutoff,
                limit = EmptyServerCleanupBatchSize,
            })).ToList();

        foreach (var serverId in serverIds)
        {
            await ShutDownServerAsync(serverId);
        }

        return serverIds.Count;
    }

    private async Task<bool> TryDeleteGameServer(Guid serverId)
    {
        await db.ExecuteAsync("DELETE FROM asset_server_player WHERE server_id = :id::uuid", new {id = serverId});
        var deletedServers = await db.ExecuteAsync("DELETE FROM asset_server WHERE id = :id::uuid", new {id = serverId});
        return deletedServers > 0; 
    }


   
    public async Task<Guid> GetJobIdByUserId(long userId)
    {
        var result = await db.QueryFirstOrDefaultAsync<Guid?>("SELECT server_id FROM asset_server_player WHERE user_id = :userId", new
        {
            userId
        });

        return result ?? throw new RecordNotFoundException("User not found in a job");
    }
    public async Task<GameServerDb> GetGameServer(Guid jobId)
    {
        return await db.QueryFirstOrDefaultAsync<GameServerDb>(
            "SELECT id, asset_id as assetId, port, updated_at as updatedAt, status, type FROM asset_server WHERE id = :id::uuid",
            new
            {
                id = jobId,
            });
    }

    public async Task<bool> IsPortTaken(int port)
    {
        int result = await db.QueryFirstOrDefaultAsync<int>(
            "SELECT port FROM asset_server WHERE port = :gsport",
            new
            {
                gsport = port,
            });
        return result != 0;
    }
    public async Task<IEnumerable<GameServerDb>> GetGameServersForPlace(long placeId, int? matchmaking = 1)
    {
        var result = await db.QueryAsync<GameServerDb>(
            @"SELECT s.id, s.asset_id AS assetId, s.port, s.updated_at AS updatedAt, s.status, s.type
          FROM asset_server s
          WHERE s.asset_id = :assetId AND s.type = :type
          ORDER BY (SELECT COUNT(*) FROM asset_server_player p WHERE p.server_id = s.id) DESC",
            new
            {
                assetId = placeId,
                type = matchmaking
            });
        if (result == null)
            return new List<GameServerDb>();
        return result;
    }

    public async Task<GameServerGetOrCreateResponse> GetServerForPlace(PlaceEntry placeInfo, int matchmaking)
    {
        var gameServers = await GetGameServersForPlace(placeInfo.placeId, matchmaking);

        foreach (var server in gameServers)
        {
            var currentPlayerCount = (await GetGameServerPlayers(server.id)).Count();

            // if the server is older than 5 minutes then shutdown the server
            if (server.updatedAt.AddMinutes(5) < DateTime.UtcNow)
            {
                await ShutDownServerAsync(server.id);
                continue;
            }

            if (currentPlayerCount >= placeInfo.maxPlayerCount)
            {
                continue;
            }

            return new GameServerGetOrCreateResponse()
            {
                job = server.id,
                ip = Configuration.GameServerIp,
                port = server.port,
                status = server.status == ServerStatus.Ready ? JoinStatus.Joining : JoinStatus.Loading
            };
        }

        // We need to create a lock to prevent multiple requests from creating the same game server
        using var serverCreationLock = await Cache.redLock.CreateLockAsync($"CreateGameServerV1", TimeSpan.FromSeconds(5));
        if (!serverCreationLock.IsAcquired)
        {
            return new GameServerGetOrCreateResponse
            {
                status = JoinStatus.Loading,
            };
        }

        Guid jobId = Guid.NewGuid();
        var startResult = await StartGameServer(placeInfo, jobId, matchmaking);

        if (startResult != null)
        {
            await db.ExecuteAsync(
                "INSERT INTO asset_server (id, asset_id, ip, port, server_connection, type) VALUES (:id::uuid, :asset_id, :ip, :port, :server_connection, :type)",
            new
            {
                id = jobId,
                asset_id = placeInfo.placeId,
                ip = Configuration.GameServerIp,
                port = startResult.proxyPort,
                server_connection = $"{Configuration.GameServerIp}:{startResult.proxyPort}",
                type = matchmaking
            });
        }
        else
        {
            return new GameServerGetOrCreateResponse()
            {
                status = JoinStatus.Error
            };
        }

        return new GameServerGetOrCreateResponse()
        {
            job = jobId,
            ip = Configuration.GameServerIp,
            port = startResult.proxyPort,
            status = JoinStatus.Waiting
        };
    }


    public async Task<ArbiterHttpClient.StartGameServerResponse?> StartGameServer(PlaceEntry placeInfo, Guid jobId, int matchmaking)
    {
        var request = ArbiterHttpClient.CreateGameServerRequest(placeInfo, jobId, matchmaking);
        return await arbiterClient.StartGameServer(request);
    }

    public async Task<IEnumerable<GameServerPlayer>> GetGameServerPlayers(Guid serverId)
    {
        return await db.QueryAsync<GameServerPlayer>(
            "SELECT user_id as userId, u.username FROM asset_server_player INNER JOIN \"user\" u ON u.id = asset_server_player.user_id WHERE server_id = :id::uuid", new
            {
               id = serverId,
            });
    }

    public async Task<IEnumerable<GameServerEntryWithPlayers>> GetGameServers(long placeId, int offset, int limit, int type = 1)
    {
        var result = (await db.QueryAsync<GameServerEntryWithPlayers>("SELECT id::uuid, asset_id as assetId FROM asset_server WHERE asset_id = :id AND type = :type LIMIT :limit OFFSET :offset", new
        {
            id = placeId,
            type,
            limit,
            offset,
        })).ToList();

        foreach (var server in result)
        {
            server.players = await GetGameServerPlayers(server.id);
        }
        return result;
    }


}
