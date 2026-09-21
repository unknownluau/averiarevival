using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace Roblox.Dto.Games;

public class GameSessionsRequest
{
    [JsonPropertyName("GameSessions")]
    public List<GameSessionDto> GameSessions { get; set; } = new();
}

public class GameSessionDto
{
    [JsonPropertyName("UserId")]
    public long UserId { get; set; }

    [JsonPropertyName("IsVr")]
    public bool IsVr { get; set; }

    [JsonPropertyName("GameTimeWhenJoined")]
    public double GameTimeWhenJoined { get; set; }

    [JsonPropertyName("GameSessionId")]
    public string GameSessionId { get; set; } = string.Empty;

    [JsonPropertyName("MembershipType")]
    public string? MembershipType { get; set; } = string.Empty;

    [JsonPropertyName("BotCheckStatus")]
    public string? BotCheckStatus { get; set; } = string.Empty;

    [JsonPropertyName("DetailedBotCheckStatus")]
    public Dictionary<string, object>? DetailedBotCheckStatus { get; set; } = new();
}