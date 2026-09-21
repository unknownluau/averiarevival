namespace Averia.RccServiceArbiter.Models;

public sealed class StartGameServerRequest
{
    public Guid JobId { get; set; }
    public long PlaceId { get; set; }
    public long UniverseId { get; set; }
    public int MaxPlayerCount { get; set; }
    public long CreatorId { get; set; }
    public long PlaceVersion { get; set; }
    public int MatchmakingContextId { get; set; }
    public long Year { get; set; }
}

public sealed class KillGameServerRequest
{
    public Guid JobId { get; set; }
}

public sealed class EvictPlayerRequest
{
    public Guid GameId { get; set; }
    public long UserId { get; set; }
    public int MessageVersionId { get; set; }
}

public sealed class SetFilteringEnabledRequest
{
    public Guid JobId { get; set; }
    public bool IsEnabled { get; set; }
}
