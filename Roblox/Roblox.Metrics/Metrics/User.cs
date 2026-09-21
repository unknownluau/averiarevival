namespace Roblox.Metrics;

public enum UserFloodCheckType { Upload, PendingAsset, Message, Follow, FriendRequest, Login }
public enum FloodCheckScope { Local, Global }
public enum SignupSource { Direct, Invite, Application }
public enum CaptchaFlow { Login }

public static class UserMetrics
{
    public static void ReportFloodCheck(UserFloodCheckType type, FloodCheckScope scope) =>
        RobloxMetrics.FloodChecks.Add(1,
            new KeyValuePair<string, object?>("flood_check.domain", "user"),
            new KeyValuePair<string, object?>("flood_check.type", TypeName(type)),
            new KeyValuePair<string, object?>("flood_check.scope", scope == FloodCheckScope.Global ? "global" : "local"));

    public static void ReportLoginConcurrentLockHit() => Record("login_concurrent_lock", null, null);
    public static void ReportSignup(SignupSource source) => Record("signup", "signup.source", source.ToString().ToLowerInvariant());
    public static void ReportCaptchaFailure(CaptchaFlow flow) => Record("captcha_failure", "captcha.flow", flow.ToString().ToLowerInvariant());
    public static void ReportLoginAttempt(bool wasSuccessful) => Record("login", "login.outcome", wasSuccessful ? "success" : "failure");
    public static void ReportApplicationDuplicateSocialUrl() => Record("application_duplicate_social_url", null, null);

    private static void Record(string eventName, string? tagName, object? tagValue)
    {
        if (tagName == null) RobloxMetrics.UserEvents.Add(1, new KeyValuePair<string, object?>("user.event", eventName));
        else RobloxMetrics.UserEvents.Add(1,
            new KeyValuePair<string, object?>("user.event", eventName),
            new KeyValuePair<string, object?>(tagName, tagValue));
    }

    private static string TypeName(UserFloodCheckType type) => type switch
    {
        UserFloodCheckType.PendingAsset => "pending_asset",
        UserFloodCheckType.FriendRequest => "friend_request",
        _ => type.ToString().ToLowerInvariant(),
    };
}
