using System.Diagnostics;
using DSharpPlus.Entities;
using Microsoft.AspNetCore.Mvc;
using Roblox.Dto.Users;
using Roblox.Libraries.Captcha;
using Roblox.Libraries.DiscordApi;
using Roblox.Logging;
using Roblox.Metrics;
using Roblox.Models.Assets;
using Roblox.Models.Users;
using Roblox.Services;
using Roblox.Services.App.FeatureFlags;
using Roblox.Services.Exceptions;
using ControllerBase = Roblox.Website.Controllers.ControllerBase;

namespace Roblox.Website.Pages.Auth;

public class Landing : RobloxPageModel
{

    public string? loginError { get; set; }
    public string? signupError { get; set; }
    public string? signupSuccess { get; set; }
    public bool loginDisabled { get; set; }
    public bool signupDisabled { get; set; }
    public DiscordUser? discordUser { get; set; }
    public string? discordAvatarUrl => discordUser != null && discordUser.AvatarHash != null
        ? $"https://cdn.discordapp.com/avatars/{discordUser.Id}/{discordUser.AvatarHash}.png?size=64"
        : null;
    public string siteKey => Configuration.HCaptchaPublicKey;

    [BindProperty]
    public string? loginUsername { get; set; }
    [BindProperty]
    public string? loginPassword { get; set; }
    [BindProperty]
    public string? loginTotpCode { get; set; }
    [FromForm(Name = "cf-turnstile-response")]
    public string? captchaResponse { get; set; }

    [BindProperty]
    public string? signupUsername { get; set; }
    [BindProperty]
    public string? signupPassword { get; set; }
    [BindProperty]
    public string? signupConfirmPassword { get; set; }
    [BindProperty]
    public string? formAction { get; set; }

    private async Task LoadDiscordUser()
    {
        if (discordAccessToken == null) return;
        try
        {
            var discordOAuth = new DiscordApi(discordAccessToken, Configuration.DiscordApplicationCallback);
            var info = await discordOAuth.GetUserInfo();
            if (info != null)
                discordUser = info;
        }
        catch (Exception)
        {
            // HttpContext.Response.Cookies.Delete("PEKORA-DISCORD");
        }
    }

    public async Task<IActionResult> OnGet()
    {
        if (userSession != null)
            return Redirect("/home");

        await LoadDiscordUser();

        try
        {
            FeatureFlags.FeatureCheck(FeatureFlag.LoginEnabled);
        }
        catch (Roblox.Services.Exceptions.RobloxException)
        {
            loginDisabled = true;
            loginError = "Login is disabled at this time. Try again later.";
        }

        try
        {
            FeatureFlags.FeatureCheck(FeatureFlag.SignupEnabled);
        }
        catch (Roblox.Services.Exceptions.RobloxException)
        {
            signupDisabled = true;
        }

        return Page();
    }

    private async Task CreateSessionAndSetCookie(long userId)
    {
        var sess = await services.users.CreateSession(userId);
        Roblox.Web.Infrastructure.Auth.RobloxSessionCookieWriter.AppendSessionCookies(
            HttpContext,
            sess,
            TimeSpan.FromDays(364));
    }

    private static async Task PreventTimingExploits(Stopwatch watch)
    {
        watch.Stop();
        const long sleepTimeMs = 200;
        var sleepTime = sleepTimeMs - watch.ElapsedMilliseconds;
        if (sleepTime is < 0 or > sleepTimeMs)
            sleepTime = 0;
        if (sleepTime != 0)
            await Task.Delay(TimeSpan.FromMilliseconds(sleepTime));
    }

    public async Task<IActionResult> OnPost()
    {
        await LoadDiscordUser();

        if (formAction == "signup")
            return await HandleSignup();

        return await HandleLogin();
    }

    private async Task<IActionResult> HandleLogin()
    {
        try
        {
            FeatureFlags.FeatureCheck(FeatureFlag.LoginEnabled);
        }
        catch (Roblox.Services.Exceptions.RobloxException)
        {
            loginDisabled = true;
            loginError = "Login is disabled at this time. Try again later.";
            return Page();
        }

        if (string.IsNullOrWhiteSpace(loginUsername))
        {
            loginError = "Empty username";
            return Page();
        }

        if (string.IsNullOrEmpty(loginPassword) || loginPassword.Length < 3)
        {
            loginError = "Empty password";
            return Page();
        }

        if (string.IsNullOrEmpty(captchaResponse))
        {
            loginError = "Your captcha could not be verified. Please try again.";
            return Page();
        }

        long userId = 0;
        try
        {
            userId = await services.users.GetUserIdFromUsername(loginUsername);
        }
        catch (RecordNotFoundException) 
        {
            loginError = "Incorrect username or password. Please try again";
            return Page();
        }

        if (!await services.cooldown.TryCooldownCheck("LoginAttemptV1:" + hashedIp, TimeSpan.FromSeconds(5)))
        {
            loginError = "Too many attempts. Try again in a few seconds.";
            UserMetrics.ReportLoginConcurrentLockHit();
            return Page();
        }

        var loginKey = "LoginAttemptCountV1:" + hashedIp;
        var attemptCount = (await services.cooldown.GetBucketDataForKey(loginKey, TimeSpan.FromMinutes(10))).ToArray();

        if (!await services.cooldown.TryIncrementBucketCooldown(loginKey, 15, TimeSpan.FromMinutes(10), attemptCount, true))
        {
            loginError = "Too many attempts. Try again in 15 minutes.";
            UserMetrics.ReportFloodCheck(UserFloodCheckType.Login, FloodCheckScope.Local);
            return Page();
        }

        if (!await HCaptcha.IsValid(rawIpAddress, captchaResponse))
        {
            loginError = "Your captcha could not be verified. Please try again.";
            UserMetrics.ReportCaptchaFailure(CaptchaFlow.Login);
            return Page();
        }

        var timer = new Stopwatch();
        timer.Start();
        if (userId == 0)
        {
            await PreventTimingExploits(timer);
            loginError = "Incorrect username or password. Please try again";
            UserMetrics.ReportLoginAttempt(false);
            return Page();
        }

        bool passwordOk = false;
        try
        {
            passwordOk = await services.users.VerifyPassword(userId, loginPassword);
        }
        catch (RecordNotFoundException)
        {
            loginError = "For security reasons your password has been expired. Please reset your password.";
            return Page();
        }

        await PreventTimingExploits(timer);
        if (!passwordOk)
        {
            loginError = "Incorrect username or password. Please try again";
            UserMetrics.ReportLoginAttempt(false);
            return Page();
        }

        if (await services.users.GetTotpStatus(userId) == Roblox.Dto.Users.TotpStatus.Enabled)
        {
            var totpInfo = await services.users.GetTotp(userId);
            if (string.IsNullOrWhiteSpace(loginTotpCode))
            {
                loginError = "You must enter a 2FA Code";
                return Page();
            }
            if (!long.TryParse(loginTotpCode, out _))
            {
                loginError = "The 2FA code you entered is not valid";
                return Page();
            }
            if (!services.users.VerifyTotp(totpInfo.secret, loginTotpCode))
            {
                loginError = "The 2FA code you entered is not valid";
                return Page();
            }
        }

        if (await services.leakCheck.IsPasswordLeaked(loginPassword))
        {
            await services.users.NullifyPassword(userId);
            await services.discordBotApi.SendMessageInChannel(Configuration.DiscordLockChannelId, $"{loginUsername} has been locked");
            loginError = "Your password has automatically expired due to it being found in a breach. Please reset your password.";
            return Page();
        }

        await CreateSessionAndSetCookie(userId);
        return Redirect("/home");
    }

    private async Task<IActionResult> HandleSignup()
    {
        try
        {
            FeatureFlags.FeatureCheck(FeatureFlag.SignupEnabled);
            FeatureFlags.FeatureCheck(FeatureFlag.ApplicationsEnabled);
        }
        catch (Roblox.Services.Exceptions.RobloxException)
        {
            signupError = "Signup is disabled at this time. Try again later.";
            signupDisabled = true;
            return Page();
        }

        if (discordAccessToken == null || discordUser == null)
        {
            signupError = "Please verify your Discord account to sign up.";
            return Page();
        }

        if (string.IsNullOrWhiteSpace(signupUsername))
        {
            signupError = "Please enter a username.";
            return Page();
        }

        if (string.IsNullOrWhiteSpace(signupPassword))
        {
            signupError = "Please enter a password.";
            return Page();
        }

        if (signupPassword != signupConfirmPassword)
        {
            signupError = "Passwords do not match.";
            return Page();
        }

        if (string.IsNullOrEmpty(captchaResponse))
        {
            signupError = "Your captcha could not be verified. Please try again.";
            return Page();
        }

        if (!await services.cooldown.TryCooldownCheck("signup:step1:" + hashedIp, TimeSpan.FromSeconds(5)))
        {
            signupError = "Too many attempts. Try again in about 5 seconds.";
            return Page();
        }

        var usernameValid = await services.users.IsUsernameValid(signupUsername);
        if (!usernameValid)
        {
            signupError = "Invalid Username. It must start and end with an alpha-numeric character, be between 3 and 20 characters, and contain at most one special character (space, period, or underscore).";
            return Page();
        }

        var nameAvailable = await services.users.IsNameAvailableForSignup(signupUsername);
        if (!nameAvailable)
        {
            signupError = "Username is already taken.";
            return Page();
        }

        var passwordValid = services.users.IsPasswordValid(signupPassword);
        if (!passwordValid)
        {
            signupError = "Password is too simple.";
            return Page();
        }

        if (await services.users.CheckDuplicateDiscord(discordUser.Id.ToString()))
        {
            signupError = "There was already an account made with this Discord account. Please try to login instead.";
            return Page();
        }

        if (!await HCaptcha.IsValid(rawIpAddress, captchaResponse))
        {
            signupError = "Your captcha could not be verified. Please try again.";
            return Page();
        }

        var signupFinalKey = "signup:step2:" + hashedIp;
        if (!await services.cooldown.TryCooldownCheck(signupFinalKey, TimeSpan.FromMinutes(5)))
        {
            signupError = "Too many attempts. Try again in about 5 minutes.";
            return Page();
        }

        if (await services.leakCheck.IsPasswordLeaked(signupPassword))
        {
            signupError = "This password was previously spotted in a leak, please choose a stronger password.";
            return Page();
        }

        string applicationId;
        string? joinId;
        try
        {
            applicationId = await services.users.CreateApplication(new CreateUserApplicationRequest
            {
                about = "Signed up",
                socialPresence = "",
                discordId = discordUser.Id.ToString(),
                discordUsername = discordUser.Username,
                isVerified = true,
                verifiedUrl = null,
                verifiedId = null,
                verificationPhrase = "",
                createdAt = DateTime.UtcNow,
                updatedAt = DateTime.UtcNow,
            });

            joinId = await services.users.ProcessApplication(applicationId, Configuration.AiUserId, UserApplicationStatus.Approved);
        }
        catch (Exception e)
        {
            Writer.Info(LogGroup.SignUp, "Error creating application: {0}", e.Message);
            signupError = "Unknown error during signup. Try again in a few minutes.";
            await services.cooldown.ResetCooldown(signupFinalKey);
            return Page();
        }

        if (joinId == null)
        {
            signupError = "Unknown error during signup. Try again in a few minutes.";
            await services.cooldown.ResetCooldown(signupFinalKey);
            return Page();
        }

        UserId createdUser;
        try
        {
            createdUser = await services.users.CreateUser(signupUsername, signupPassword, Gender.Unknown);
        }
        catch (Exception)
        {
            await services.cooldown.ResetCooldown(signupFinalKey);
            throw;
        }

        await services.users.SetApplicationUserIdByJoinId(joinId, createdUser.userId);

        await CreateSessionAndSetCookie(createdUser.userId);

        if (FeatureFlags.IsEnabled(FeatureFlag.CreatePlaceSelfService))
        {
            var asset = await services.assets.CreatePlace(createdUser.userId, signupUsername, CreatorType.User, createdUser.userId);
            await services.games.CreateUniverse(asset.placeId);
        }

        // HttpContext.Response.Cookies.Delete("PEKORA-DISCORD");

        return Redirect("/home");
    }
}
