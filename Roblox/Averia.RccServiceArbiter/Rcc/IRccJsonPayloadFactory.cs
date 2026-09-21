using Averia.RccServiceArbiter.Models;

namespace Averia.RccServiceArbiter.Rcc;

public interface IRccJsonPayloadFactory
{
    string CreateGameServerPayload(StartGameServerRequest request, int gameServerPort);
}
