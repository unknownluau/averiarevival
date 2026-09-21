namespace Roblox.Metrics;

public enum GameVoteFloodCheckType { Short, Long, Asset }
public enum GameJoinStage { Attempt, PlaceLauncherSuccess, Success }
public enum GameServerOperation { GetServerInfo, StartServer }

public static class GameMetrics
{
    public static void ReportVoteFloodCheck(GameVoteFloodCheckType type) => RobloxMetrics.FloodChecks.Add(1,
        new KeyValuePair<string, object?>("flood_check.domain", "game_vote"),
        new KeyValuePair<string, object?>("flood_check.type", type.ToString().ToLowerInvariant()),
        new KeyValuePair<string, object?>("flood_check.scope", "local"));

    public static void ReportJoinStage(GameJoinStage stage) => RobloxMetrics.GameJoinEvents.Add(1,
        new KeyValuePair<string, object?>("game.join.stage", stage switch
        {
            GameJoinStage.PlaceLauncherSuccess => "place_launcher_success",
            _ => stage.ToString().ToLowerInvariant(),
        }));

    public static void ReportRccAuthorizationFailure() => RecordServerEvent("rcc_authorization_failure");
    public static void ReportTicketUserMismatch() => RecordServerEvent("ticket_user_mismatch");
    public static void ReportServerShutdownWithoutDatabaseEntry() => RecordServerEvent("shutdown_without_database_entry");

    public static void ReportServerOperationDuration(GameServerOperation operation, long elapsedMilliseconds) =>
        RobloxMetrics.GameServerDuration.Record(elapsedMilliseconds,
            new KeyValuePair<string, object?>("game.server.operation", operation == GameServerOperation.StartServer ? "start" : "get_info"));

    private static void RecordServerEvent(string eventName) => RobloxMetrics.GameServerEvents.Add(1,
        new KeyValuePair<string, object?>("game.server.event", eventName));
}
