namespace Roblox.Metrics;

public static class AssetMetrics
{
    public static void ReportInvalidClothingImageFormatUploadAttempt(string formatName) =>
        Record("invalid_image_format", NormalizeFormat(formatName));

    public static void ReportInvalidClothingFileUploadAttempt() => Record("invalid_file", null);

    private static void Record(string reason, string? format) => RobloxMetrics.AssetUploadFailures.Add(1,
        new KeyValuePair<string, object?>("failure.reason", reason),
        new KeyValuePair<string, object?>("asset.format", format ?? "unknown"));

    private static string NormalizeFormat(string? format) => string.IsNullOrWhiteSpace(format)
        ? "unknown"
        : format.Trim().ToLowerInvariant() is var value && value.Length <= 32 ? value : "other";
}
