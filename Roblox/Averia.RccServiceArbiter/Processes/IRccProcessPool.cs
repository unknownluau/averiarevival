using Averia.RccServiceArbiter.Models;

namespace Averia.RccServiceArbiter.Processes;

public interface IRccProcessPool
{
    Task<StartGameServerResponse> StartGameServerAsync(StartGameServerRequest request, CancellationToken cancellationToken);
    Task<bool> StopGameServerAsync(Guid jobId, CancellationToken cancellationToken);
    Task<bool> EvictPlayerAsync(Guid jobId, long userId, int messageVersionId, CancellationToken cancellationToken);
    Task<bool> SetFilteringEnabledAsync(Guid jobId, bool isEnabled, CancellationToken cancellationToken);
    Task<bool> RunGlobalMessageAsync(Guid jobId, string topic, CancellationToken cancellationToken);
    Task<int> CleanUpAsync(CancellationToken cancellationToken);
    ArbiterStatisticsResponse GetStatistics();
}
