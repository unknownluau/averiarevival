using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Roblox.Dto.Users;
using Roblox.Exceptions;
using Roblox.Libraries.Captcha;
using Roblox.Libraries.EasyJwt;
using Roblox.Logging;
using Roblox.Models.Users;
using Roblox.Services;
using Roblox.Services.App.FeatureFlags;
using Roblox.Services.Exceptions;
using Roblox.Website.Filters;
using Roblox.Website.WebsiteServices;

namespace Roblox.Website.Pages.Auth;

// TODO: are we exposing too much info? should we expect the user to provide their social media url, then make sure it matches the one stored in the db?
public class PasswordReset : RobloxPageModel
{
    private const string InvalidUsernameMessage = "The username specified is invalid.";
    private const string UnsupportedSiteException = "The site used to verify your account is currently not supported for password resets. We plan to add support for more sites in the future. Check back later. Please do not contact support as they cannot help you.";
    private const string MissingVerificationUrl = "The social media URL used to create your account is no longer valid. You cannot reset your password.";
    private const string VerificationIdChanged = "The ID of the social media account this username belonged to has changed. You can no longer change the password for this account.";
    private const string CannotGenerateVerificationPhrase = "Unable to generate verification phrase.";
    private const string CannotFindPhrase = "Could not find the phrase on your social media profile. You may have to wait a few minutes and try again.";
    private const string Cooldown = "Too many attempts. Try again in a few minutes.";
    private const string InvalidPasswordResetId = "This form is no longer valid. Please refresh the page and try again.";
    private const string InvalidNewPassword = "Your new password is invalid. It must be at least 3 characters.";
    private const string LeakedPassword = "This password was previously spotted in a leak, please choose a stronger password.";
    private const string DiscordPasswordResetSent = "If this account can use Discord password reset, we sent a reset link to the Discord account it signed up with.";
    private const string DiscordPasswordResetFailed = "We could not send a Discord DM for this password reset. Make sure your DMs are open and try again.";

    [BindProperty]
    public string? username { get; set; }
    [BindProperty]
    public string? action { get; set; }
    [FromForm(Name = "cf-turnstile-response")]
    public string hCaptchaResponse { get; set; }
    public string siteKey => Configuration.HCaptchaPublicKey;
    public string? errorMessage { get; set; }
    public string? successMessage { get; set; }
    public string? verificationPhrase { get; private set; }
    [BindProperty(SupportsGet = true)]
    public string? passwordResetId { get; set; }
    [BindProperty]
    public string? newPassword { get; set; }

    private bool IsEnabled()
    {
        return FeatureFlags.IsEnabled(FeatureFlag.PasswordReset);
    }

    public async Task<IActionResult> OnGet()
    {
        if (!IsEnabled())
            return new RedirectResult("/");

        if (!string.IsNullOrWhiteSpace(passwordResetId) && await GetValidPasswordResetEntry(passwordResetId) == null)
        {
            passwordResetId = null;
            errorMessage = InvalidPasswordResetId;
        }

        return new PageResult();
    }

    private async Task<bool> TryGenerateCode()
    {
        var apps = new ApplicationWebsiteService(HttpContext);

            Writer.Info(LogGroup.AbuseDetection, "Generate code for PasswordReset");
            try
            {
                verificationPhrase = await apps.ApplyVerificationPhrase(hashedIp, ApplicationService.GenerationContext.PasswordReset);
            }
            catch (TooManyRequestsException)
            {
                errorMessage = "Too many attempts to generate a verification phrase. Make sure you have cookies enabled, then try again in a few minutes.";
                return false;
            }

            return true;

    }

    public async Task<IActionResult> OnPost()
    {
        if (!IsEnabled())
            return new RedirectResult("/");

        if (action == "change")
        {
            return await HandlePasswordChange();
        }

        if (string.IsNullOrWhiteSpace(username))
        {
            errorMessage = InvalidUsernameMessage;
            return new PageResult();
        }

        long userId;
        try
        {
            userId = await services.users.GetUserIdFromUsername(username);
        }
        catch (RecordNotFoundException)
        {
            errorMessage = InvalidUsernameMessage;
            return new PageResult();
        }
        //staff just needs to msg me to reset pw
        if(await StaffFilter.IsStaff(userId))
        {
            errorMessage = InvalidUsernameMessage;
            return new PageResult();
        }
        var app = await services.users.GetApplicationByUserId(userId);
        if (app == null)
        {
            errorMessage = InvalidUsernameMessage;
            return new PageResult();
        }
        
        if(!await services.cooldown.TryIncrementBucketCooldown("PasswordResetIP:V1:"+hashedIp, 3, TimeSpan.FromHours(1)) || 
           !await services.cooldown.TryIncrementBucketCooldown("PasswordResetUserID:V1:"+userId, 3, TimeSpan.FromMinutes(10)))
        {
            errorMessage = Cooldown;
            return new PageResult();
        }
        
        // check captcha
        var userIp = Roblox.Website.Controllers.ControllerBase.GetRequesterIpRaw(HttpContext);
        if (!await HCaptcha.IsValid(userIp, hCaptchaResponse))
        {
            errorMessage = "Your captcha could not be verified. Please try again.";
            return new PageResult();
        }

        var discordId = app.discordId;
        if (string.IsNullOrWhiteSpace(discordId))
        {
            // old behavior
            var url = app?.socialPresence ?? app?.verifiedUrl;
            if (app == null || string.IsNullOrWhiteSpace(url))
            {
                errorMessage = MissingVerificationUrl;
                return new PageResult();
            }

            if (!await TryGenerateCode())
                return new PageResult();

            if (verificationPhrase == null)
            {
                errorMessage = CannotGenerateVerificationPhrase;
                return new PageResult();
            }

            switch (action)
            {
                case "verify":
                {
                    var apps = new ApplicationWebsiteService(HttpContext);

                    VerificationResult result;
                    try
                    {
                        result = await apps.AttemptVerifyUser(url, verificationPhrase);
                    }
                    catch (InvalidSocialMediaUrlException)
                    {
                        errorMessage = UnsupportedSiteException;
                        return new PageResult();
                    }
                    catch (UnableToFindVerificationPhraseException)
                    {
                        errorMessage = CannotFindPhrase;
                        return new PageResult();
                    }

                    if (!result.isVerified)
                    {
                        errorMessage = UnsupportedSiteException;
                        return new PageResult();
                    }

                    // app.verifiedId can be null for old apps - only validate this on new apps
                    // TODO: security considerations? are there many apps with twitter usernames that were changed?
                    if (result.verifiedId != app.verifiedId && !string.IsNullOrWhiteSpace(app.verifiedId))
                    {
                        errorMessage = VerificationIdChanged;
                        return new PageResult();
                    }

                    // We are verified
                    passwordResetId = await services.users.CreatePasswordResetEntry(userId, url, verificationPhrase);
                    break;
                }
            }
            
            return new PageResult();
        }

        var newPasswordResetId = await services.users.CreatePasswordResetEntry(userId, discordId, "0000");
        var sent = await services.discordBotApi.MessageUser(
            discordId,
            $"https://www.{Configuration.ShortBaseUrl}/auth/password-reset?passwordResetId={newPasswordResetId}\nIf you did not request this password reset, you may simply ignore.");
        if (!sent)
        {
            await services.users.DeletePasswordResetEntry(newPasswordResetId);
            errorMessage = DiscordPasswordResetFailed;
            return new PageResult();
        }

        successMessage = DiscordPasswordResetSent;

        return new PageResult();
    }

    private async Task<IActionResult> HandlePasswordChange()
    {
        if (string.IsNullOrWhiteSpace(passwordResetId) || !Guid.TryParse(passwordResetId, out _))
        {
            errorMessage = InvalidPasswordResetId;
            return new PageResult();
        }

        if (string.IsNullOrWhiteSpace(newPassword) || newPassword.Length < 3)
        {
            errorMessage = InvalidNewPassword;
            return new PageResult();
        }

        var userIp = Roblox.Website.Controllers.ControllerBase.GetRequesterIpRaw(HttpContext);
        if (!await HCaptcha.IsValid(userIp, hCaptchaResponse))
        {
            errorMessage = "Your captcha could not be verified. Please try again.";
            return new PageResult();
        }

        if (await services.leakCheck.IsPasswordLeaked(newPassword))
        {
            errorMessage = LeakedPassword;
            return new PageResult();
        }

        await using var redemptionLock = await services.users.GetPasswordResetLock(passwordResetId);
        var data = await GetValidPasswordResetEntry(passwordResetId);
        if (data == null || await StaffFilter.IsStaff(data.userId))
        {
            errorMessage = InvalidPasswordResetId;
            return new PageResult();
        }

        if (!await services.users.RedeemPasswordReset(passwordResetId, newPassword))
        {
            errorMessage = InvalidPasswordResetId;
            return new PageResult();
        }

        passwordResetId = null;
        successMessage = "Your password has been successfully updated.";
        return new PageResult();
    }

    private async Task<PasswordResetEntry?> GetValidPasswordResetEntry(string id)
    {
        var data = await services.users.GetPasswordResetEntry(id);
        if (data == null ||
            data.createdAt < DateTime.UtcNow.AddHours(-1) ||
            data.status != PasswordResetState.Created)
        {
            return null;
        }

        return data;
    }
}
