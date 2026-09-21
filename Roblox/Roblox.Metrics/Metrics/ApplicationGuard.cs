namespace Roblox.Metrics;

public static class ApplicationGuardMetrics
{
    public static void ReportBlockedUserAgent() => Record("blocked");
    public static void ReportAllowedUserAgent() => Record("allowed");
    public static void ReportCaptchaSuccess() => Record("captcha_completed");

    private static void Record(string outcome) => RobloxMetrics.ApplicationGuardEvents.Add(1,
        new KeyValuePair<string, object?>("guard.outcome", outcome));
}
