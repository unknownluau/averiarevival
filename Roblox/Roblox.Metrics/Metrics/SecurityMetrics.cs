namespace Roblox.Metrics;

public static class SecurityMetrics
{
    public static void ReportBadCharacterFoundInAssetContentName(string method) => Record("bad_asset_content_character", method);
    public static void ReportErrorDeletingAssetContent() => Record("asset_content_delete_error", null);

    private static void Record(string eventName, string? operation) => RobloxMetrics.SecurityEvents.Add(1,
        new KeyValuePair<string, object?>("security.event", eventName),
        new KeyValuePair<string, object?>("security.operation", NormalizeOperation(operation)));

    private static string NormalizeOperation(string? value) => value switch
    {
        "GetAssetContent" => "get_asset_content",
        "DeleteAssetContent" => "delete_asset_content",
        _ => "unknown",
    };
}
