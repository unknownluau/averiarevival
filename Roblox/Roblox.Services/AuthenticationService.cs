using System.Text.Json;
using System.Web;
using Roblox.Dto.Authentication;
using Roblox.Dto.Users;
using Roblox.Libraries.DiscordApi;
using Roblox.Libraries.LeakCheckApi;
using Roblox.Logging;
using Roblox.Services.App.FeatureFlags;
using Roblox.Services.Exceptions;

namespace Roblox.Services;

public class AuthenticationService : ServiceBase, IService
{
    private static readonly JsonSerializerOptions LoginJsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    public async Task<LoginV1ServiceResult> LoginV1(LoginRequest request, LoginRequestContext context)
    {
        await ValidateLoginRequest(context);
        
        var username = string.IsNullOrEmpty(request.username) && request.ctype == "Username"
            ? request.cvalue
            : request.username;

        if (string.IsNullOrEmpty(username) || string.IsNullOrEmpty(request.password))
        {
            throw BadRequest(
                (int)LoginError400.UsernamePasswordRequired,
                "Username and Password are required. Please try again.");
        }

        var credential = SplitUsernameAndTotpCode(username);
        using var users = ServiceProvider.GetOrCreate<UsersService>(this);
        var userInfo = await users.GetUserByName(credential.username);

        await VerifyLoginCredentials(
            users,
            userInfo.username,
            request.password,
            userInfo.userId,
            credential.totpCode,
            context.isPasswordLeaked,
            skipTwoFactor: false);

        return new LoginV1ServiceResult
        {
            sessionId = await users.CreateSession(userInfo.userId),
            response = new LoginV1Response
            {
                user = CreateUserResponse(userInfo.userId, userInfo.username),
                isBanned = userInfo.IsDeleted(),
            },
        };
    }

    public async Task<LoginV2ServiceResult> LoginV2(string requestBody, LoginRequestContext context)
    {
        if (string.IsNullOrEmpty(requestBody))
        {
            throw BadRequest((int)LoginError400.CredentialTypeNotSupported, "Empty request body.");
        }

        var parsedRequest = ParseLoginV2Request(requestBody, context.userAgent);
        var username = string.IsNullOrEmpty(parsedRequest.username) && parsedRequest.ctype == "Username"
            ? parsedRequest.cvalue
            : parsedRequest.username;

        if (string.IsNullOrEmpty(username) || string.IsNullOrEmpty(parsedRequest.password))
        {
            throw BadRequest(
                (int)LoginError400.UsernamePasswordRequired,
                "Username and Password are required. Please try again.");
        }

        await ValidateLoginRequest(context);

        var credential = SplitUsernameAndTotpCode(username);
        using var users = ServiceProvider.GetOrCreate<UsersService>(this);
        var userInfo = await users.GetUserByName(credential.username);

        await VerifyLoginCredentials(
            users,
            credential.username,
            parsedRequest.password,
            userInfo.userId,
            credential.totpCode,
            context.isPasswordLeaked,
            skipTwoFactor: true);

        if (await users.GetTotpStatus(userInfo.userId) == TotpStatus.Enabled)
        {
            var ticket = await users.Generate2SVTicket(new TwoFactorTicket
            {
                userId = userInfo.userId,
                hashedIp = context.hashedIp,
            });

            return new LoginV2ServiceResult
            {
                twoStepVerificationResponse = new LoginV2TwoStepRequiredResponse
                {
                    message = "TwoStepVerificationRequired",
                    mediaType = "Email",
                    tl = ticket,
                    code = 6,
                    twoStepVerificationData = new LoginTwoStepVerificationData
                    {
                        mediaType = "Email",
                        ticket = ticket,
                    },
                    identityVerificationLoginTicket = ticket,
                    user = CreateUserResponse(userInfo.userId, userInfo.username),
                },
            };
        }

        return new LoginV2ServiceResult
        {
            sessionId = await users.CreateSession(userInfo.userId),
            response = new LoginV2Response
            {
                membershipType = 4,
                username = userInfo.username,
                name = userInfo.username,
                isUnder13 = false,
                countryCode = "US",
                userId = userInfo.userId,
                id = userInfo.userId,
                displayName = userInfo.username,
                user = CreateUserResponse(userInfo.userId, userInfo.username),
                isBanned = false,
            },
        };
    }

    private static LoginRequest ParseLoginV2Request(string requestBody, string userAgent)
    {
        if (ShouldParseAsFormUrlEncoded(requestBody, userAgent))
        {
            return ParseFormUrlEncodedLoginRequest(requestBody);
        }

        try
        {
            return JsonSerializer.Deserialize<LoginRequest>(requestBody, LoginJsonOptions) ?? new LoginRequest();
        }
        catch (JsonException)
        {
            Writer.Info(LogGroup.Authentication, "Failed to parse v2 login request body.");
            return new LoginRequest();
        }
    }

    private static bool ShouldParseAsFormUrlEncoded(string requestBody, string userAgent)
    {
        if (userAgent == "RobloxStudio/WinInet")
        {
            return true;
        }

        var trimmedRequestBody = requestBody.TrimStart();
        return !trimmedRequestBody.StartsWith('{') &&
               !trimmedRequestBody.StartsWith('[') &&
               requestBody.Contains('=') &&
               (requestBody.Contains("username=", StringComparison.OrdinalIgnoreCase) ||
                requestBody.Contains("password=", StringComparison.OrdinalIgnoreCase) ||
                requestBody.Contains("cvalue=", StringComparison.OrdinalIgnoreCase));
    }

    private static LoginRequest ParseFormUrlEncodedLoginRequest(string requestBody)
    {
        var form = HttpUtility.ParseQueryString(requestBody);
        return new LoginRequest
        {
            username = FirstNonEmpty(form["username"], form["cvalue"]),
            ctype = form["ctype"] ?? string.Empty,
            cvalue = form["cvalue"] ?? string.Empty,
            password = form["password"] ?? string.Empty,
        };
    }

    private static string? FirstNonEmpty(params string?[] values)
    {
        return values.FirstOrDefault(value => !string.IsNullOrEmpty(value));
    }

    private async Task ValidateLoginRequest(LoginRequestContext context)
    {
        FeatureFlags.FeatureCheck(FeatureFlag.LoginEnabled);

        var loginKey = "LoginAttemptCountV1:" + context.hashedIp;
        using var cooldown = ServiceProvider.GetOrCreate<CooldownService>(this);
        var attemptCount = (await cooldown.GetBucketDataForKey(loginKey, TimeSpan.FromMinutes(10))).ToArray();

        if (!await cooldown.TryIncrementBucketCooldown(loginKey, 15, TimeSpan.FromMinutes(10), attemptCount, true))
        {
            throw Forbidden((int)LoginError403.TooManyAttempts, "Too many attempts please wait 10 minutes before trying again.");
        }

        /*if (!context.isRobloxClient)
        {
            throw Forbidden((int)LoginError403.IncorrectCredentials, "Incorrect username or password. Please try again");
        }*/
    }

    private async Task VerifyLoginCredentials(
        UsersService users,
        string username,
        string password,
        long userId,
        string? totpCode,
        bool isPasswordLeaked,
        bool skipTwoFactor)
    {
        if (!await users.VerifyPassword(userId, password))
        {
            throw Forbidden((int)LoginError403.IncorrectCredentials, "Incorrect username or password. Please try again");
        }

        if (isPasswordLeaked && await IsPasswordLeaked(password))
        {
            await users.NullifyPassword(userId);
            await SendLockMessage(username);
            throw Forbidden((int)LoginError403.IncorrectCredentials, "Incorrect username or password. Please try again");
        }

        if (skipTwoFactor)
        {
            return;
        }

        if (await users.GetTotpStatus(userId) != TotpStatus.Enabled)
        {
            return;
        }

        var totpInfo = await users.GetTotp(userId);
        if (totpInfo == null || string.IsNullOrEmpty(totpCode))
        {
            throw Forbidden((int)LoginError403.IncorrectCredentials, $"You have 2FA enabled. Please login with this username format {username}|2FA Code");
        }

        if (!users.VerifyTotp(totpInfo.secret, totpCode))
        {
            throw Forbidden((int)LoginError403.IncorrectCredentials, "Incorrect 2FA code. Please try again.");
        }
    }

    private static async Task<bool> IsPasswordLeaked(string password)
    {
        var leakCheck = new LeakCheckApi(Roblox.Configuration.LeakCheckApiKey);
        try
        {
            return await leakCheck.IsPasswordLeaked(password);
        }
        finally
        {
            leakCheck.Dispose();
        }
    }

    private static async Task SendLockMessage(string username)
    {
        var discordBotApi = new DiscordBotApi(Roblox.Configuration.DiscordBotToken);
        await discordBotApi.SendMessageInChannel(Roblox.Configuration.DiscordLockChannelId, $"{username} has been locked");
    }

    private static (string username, string totpCode) SplitUsernameAndTotpCode(string username)
    {
        var splitUsername = username.Split('|');
        return (splitUsername[0], splitUsername.Length == 2 ? splitUsername[1] : string.Empty);
    }

    private static LoginUserResponse CreateUserResponse(long userId, string username)
    {
        return new LoginUserResponse
        {
            id = userId,
            name = username,
            displayName = username,
        };
    }

    private static RobloxException BadRequest(int errorCode, string message)
    {
        return new RobloxException(RobloxException.BadRequest, errorCode, message);
    }

    private static RobloxException Forbidden(int errorCode, string message)
    {
        return new RobloxException(RobloxException.Forbidden, errorCode, message);
    }

    public bool IsThreadSafe()
    {
        return true;
    }

    public bool IsReusable()
    {
        return false;
    }
}
