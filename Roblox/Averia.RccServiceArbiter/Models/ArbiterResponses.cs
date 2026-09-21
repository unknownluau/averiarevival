namespace Averia.RccServiceArbiter.Models;

public sealed class StartGameServerResponse
{
    public string Status { get; set; } = "Started";
    public Guid JobId { get; set; }
    public int RccPort { get; set; }
    public int GameServerPort { get; set; }
    public int ProxyPort { get; set; }
    public int? RccProcessId { get; set; }
    public int? QuilkinProcessId { get; set; }
}

public sealed class ArbiterActionResponse
{
    public bool Success { get; set; }
}

public sealed class ArbiterStatisticsResponse
{
    public int ServerCount { get; set; }
    public IReadOnlyDictionary<Guid, ArbiterServerStatistics> Servers { get; set; } =
        new Dictionary<Guid, ArbiterServerStatistics>();
}

public sealed class ArbiterServerStatistics
{
    public long Year { get; set; }
    public int RccPort { get; set; }
    public int GameServerPort { get; set; }
    public int ProxyPort { get; set; }
    public int? RccProcessId { get; set; }
    public int? QuilkinProcessId { get; set; }
    public int UseCount { get; set; }
    public DateTime ExpirationUtc { get; set; }
}
