namespace Roblox.Dto.Authentication;

public class LoginRequest
{
    public string? username { get; set; }
    public string ctype { get; set; } = string.Empty;
    public string cvalue { get; set; } = string.Empty;
    public string password { get; set; } = string.Empty;
}

public class LegacyLoginRequest
{
    public string username { get; set; } = string.Empty;
    public string password { get; set; } = string.Empty;
}

public sealed class LoginRequestContext
{
    public string hashedIp { get; set; } = string.Empty;
    public string userAgent { get; set; } = string.Empty;
    public bool isRobloxClient { get; set; }
    public bool isPasswordLeaked { get; set; }
}

public sealed class LoginUserResponse
{
    public long id { get; set; }
    public string name { get; set; } = string.Empty;
    public string displayName { get; set; } = string.Empty;
}

public sealed class LoginV1Response
{
    public LoginUserResponse user { get; set; } = new();
    public bool isBanned { get; set; }
}

public sealed class LoginV2Response
{
    public int membershipType { get; set; }
    public string username { get; set; } = string.Empty;
    public string name { get; set; } = string.Empty;
    public bool isUnder13 { get; set; }
    public string countryCode { get; set; } = string.Empty;
    public long userId { get; set; }
    public long id { get; set; }
    public string displayName { get; set; } = string.Empty;
    public LoginUserResponse user { get; set; } = new();
    public bool isBanned { get; set; }
}

public sealed class LoginTwoStepVerificationData
{
    public string mediaType { get; set; } = string.Empty;
    public string ticket { get; set; } = string.Empty;
}

public sealed class LoginV2TwoStepRequiredResponse
{
    public string message { get; set; } = string.Empty;
    public string mediaType { get; set; } = string.Empty;
    public string tl { get; set; } = string.Empty;
    public int code { get; set; }
    public LoginTwoStepVerificationData twoStepVerificationData { get; set; } = new();
    public string identityVerificationLoginTicket { get; set; } = string.Empty;
    public LoginUserResponse user { get; set; } = new();
}

public sealed class PasswordStatusResponse
{
    public bool valid { get; set; }
}

public sealed class LoginV1ServiceResult
{
    public string sessionId { get; set; } = string.Empty;
    public LoginV1Response response { get; set; } = new();
}

public sealed class LoginV2ServiceResult
{
    public string? sessionId { get; set; }
    public LoginV2Response? response { get; set; }
    public LoginV2TwoStepRequiredResponse? twoStepVerificationResponse { get; set; }
    public bool requiresTwoStepVerification => twoStepVerificationResponse != null;
}

public sealed class TwoStepEmailVerifyResponse
{
    public string verificationToken { get; set; } = string.Empty;
}

public sealed class TwoStepLegacyLoginResponse
{
    public long userId { get; set; }
}
