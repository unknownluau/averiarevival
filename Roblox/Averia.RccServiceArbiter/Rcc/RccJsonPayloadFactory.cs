using System.Text.Json;
using Averia.RccServiceArbiter.Configuration;
using Averia.RccServiceArbiter.Models;
using Microsoft.Extensions.Options;

namespace Averia.RccServiceArbiter.Rcc;

public sealed class RccJsonPayloadFactory : IRccJsonPayloadFactory
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = null,
    };

    private readonly ArbiterOptions _options;

    public RccJsonPayloadFactory(IOptions<ArbiterOptions> options)
    {
        _options = options.Value;
    }

    public string CreateGameServerPayload(StartGameServerRequest request, int gameServerPort)
    {
        var baseUrl = _options.BaseUrl.TrimEnd('/');
        return JsonSerializer.Serialize(new
        {
            Mode = "GameServer",
            Settings = new
            {
                PlaceId = request.PlaceId,
                CreatorId = request.CreatorId,
                GameId = request.JobId.ToString(),
                MachineAddress = _options.PublicIp,
                MaxPlayers = request.MaxPlayerCount,
                GsmInterval = 10,
                MaxGameInstances = 5,
                PreferredPlayerCapacity = Math.Min(request.MaxPlayerCount, 10),
                UniverseId = request.UniverseId,
                BaseUrl = _options.ServiceUrl,
                PlaceFetchUrl = $"{baseUrl}/asset?id={request.PlaceId}",
                MatchmakingContextId = request.MatchmakingContextId,
                CreatorType = "User",
                PlaceVersion = request.PlaceVersion,
                JobId = request.JobId.ToString(),
                PreferredPort = gameServerPort,
                ApiKey = _options.GameServerApiKey,
                PlaceVisitAccessKey = _options.PlaceVisitAccessKey,
            },
        }, JsonOptions);
    }
}
