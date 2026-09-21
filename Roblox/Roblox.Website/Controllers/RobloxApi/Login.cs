using Microsoft.AspNetCore.Mvc;
using MVC = Microsoft.AspNetCore.Mvc;
using Roblox.Dto.Authentication;
using Roblox.Dto.Users;
using Roblox.Exceptions;
using Roblox.Logging;
using Roblox.Services.Exceptions;
using Roblox.Web.Infrastructure.Auth;
using TwoFactor = Roblox.Website.WebsiteModels.Authentication.TwoFactor;
using TwoFactorEmail = Roblox.Website.WebsiteModels.Authentication.TwoFactorEmail;
using TwoFactorEmailLogin = Roblox.Website.WebsiteModels.Authentication.TwoFactorEmailLogin;
using TwoFactorLegacy = Roblox.Website.WebsiteModels.Authentication.TwoFactorLegacy;
using BadRequestException = Roblox.Exceptions.BadRequestException;

namespace Roblox.Website.Controllers
{
    [MVC.ApiController]
    [MVC.Route("/")]
    public class RobloxLogin : ControllerBase
    {
        [HttpPostBypass("v1/login")]
        public async Task<LoginV1Response> LoginV1([FromBody] LoginRequest request)
        {
            var result = await services.authentication.LoginV1(request, CreateLoginRequestContext());
            RobloxSessionCookieWriter.AppendSessionCookies(HttpContext, result.sessionId);
            return result.response;
        }

        [HttpPostBypass("v2/login")]
        public async Task<IActionResult> LoginV2()
        {
            var result = await services.authentication.LoginV2(await GetRequestBody(), CreateLoginRequestContext());

            if (result.requiresTwoStepVerification)
            {
                return Ok(result.twoStepVerificationResponse);
            }

            RobloxSessionCookieWriter.AppendSessionCookies(HttpContext, result.sessionId!);
            return Ok(result.response);
        }

        [HttpPostBypass("v3/users/{userId:long}/two-step-verification/login")]
        public async Task<IActionResult> TwoStepVerificationEmailLogin([FromRoute] long userId, [FromBody] TwoFactorEmailLogin request)
        {
            LoginTicet ticketInfo = await services.users.GetLoginTicketInfo(request.verificationToken);

            if (ticketInfo.userId != userId || ticketInfo.challengeId != request.challengeId)
                throw new BadRequestException(5, "Invalid two step verification ticket.");

            if (ticketInfo.hashedIp != GetIP())
                throw new BadRequestException(5, "Invalid login locaton");

            Writer.Info(LogGroup.Authentication, "User {0} has logged in with 2FA.", userId);

            await services.users.DeleteTicket(request.verificationToken);
            var sessionId = await services.users.CreateSession(ticketInfo.userId);
            RobloxSessionCookieWriter.AppendSessionCookies(HttpContext, sessionId);

            return Content("{}", "application/json");
        }

        [HttpPostBypass("/v1/users/{userId}/challenges/email/verify")]
        public async Task<TwoStepEmailVerifyResponse> TwoStepVerificationEmail([FromRoute] long userId, [FromBody] TwoFactorEmail request)
        {
            TwoFactorTicket info;
            try
            {
                info = await services.users.GetInfoFrom2SVTicket(request.challengeId);
                // Security check PARANOIA!
                if (info.userId != userId || info.hashedIp != GetIP())
                    throw new BadRequestException(5, "Invalid two step verification ticket.");

                if (await services.users.GetTotpStatus(info.userId) != TotpStatus.Enabled)
                    throw new BadRequestException(6, "Failure2SVNotEnabled");

                if (request.code == null)
                    throw new BadRequestException(6, "Failure2SVInvalidCode");

                var totpInfo = await services.users.GetTotp(info.userId);
                if (totpInfo == null || !services.users.VerifyTotp(totpInfo.secret, request.code))
                    throw new BadRequestException(6, "Failure2SVInvalidCode");
            }
            catch (RecordNotFoundException)
            {
                throw new BadRequestException(5, "Invalid two step verification ticket.");
            }

            await services.users.DeleteTicket(request.challengeId);
            LoginTicet loginTicketInfo = new LoginTicet
            {
                userId = userId,
                challengeId = request.challengeId,
                hashedIp = GetIP(),
            };

            return new TwoStepEmailVerifyResponse
            {
                verificationToken = await services.users.GenerateLoginTicket(loginTicketInfo),
            };
        }

        [HttpPostBypass("v1/twostepverification/verify")]
        [HttpPostBypass("v2/twostepverification/verify")]
        public async Task<IActionResult> TwoStepVerification([FromBody] TwoFactor request)
        {
            TwoFactorTicket info;
            try
            {
                info = await services.users.GetInfoFrom2SVTicket(request.ticket);
                UserInfo userInfo = await services.users.GetUserById(info.userId);
                if (userInfo.username != request.username || info.hashedIp != GetIP())
                    throw new RecordNotFoundException();

                if (await services.users.GetTotpStatus(info.userId) != TotpStatus.Enabled)
                    throw new BadRequestException(6, "Failure2SVNotEnabled");

                TotpInfo? totpInfo = await services.users.GetTotp(info.userId);

                if (totpInfo == null || !services.users.VerifyTotp(totpInfo.secret, request.code))
                    throw new BadRequestException(6, "Failure2SVInvalidCode");
            }
            catch (RecordNotFoundException)
            {
                throw new BadRequestException(5, "Invalid two step verification ticket.");
            }

            await services.users.DeleteTicket(request.ticket);
            var sessionId = await services.users.CreateSession(info.userId);
            RobloxSessionCookieWriter.AppendSessionCookies(HttpContext, sessionId);

            return Content("{}", "application/json");
        }

        [HttpPostBypass("v2/twostepverification/login/verify")]
        public async Task<TwoStepLegacyLoginResponse> TwoStepVerificationLegacy([FromBody] TwoFactorLegacy request)
        {
            TwoFactorTicket info;
            try
            {
                info = await services.users.GetInfoFrom2SVTicket(request.tl);
                UserInfo userInfo = await services.users.GetUserById(info.userId);
                if (userInfo.username != request.username)
                    throw new RecordNotFoundException();

                if (await services.users.GetTotpStatus(info.userId) != TotpStatus.Enabled)
                    throw new BadRequestException(6, "2FA is not enabled on this account.");

                TotpInfo? totpInfo = await services.users.GetTotp(info.userId);

                if (totpInfo == null || !services.users.VerifyTotp(totpInfo.secret, request.identificationCode))
                    throw new BadRequestException(6, "Incorrect 2FA code. Please try again.");
            }
            catch (RecordNotFoundException)
            {
                throw new BadRequestException(5, "Invalid two step verification ticket.");
            }

            await services.users.DeleteTicket(request.tl);
            var sessionId = await services.users.CreateSession(info.userId);
            RobloxSessionCookieWriter.AppendSessionCookies(HttpContext, sessionId);

            return new TwoStepLegacyLoginResponse
            {
                userId = info.userId,
            };
        }

        [HttpGetBypass("v2/passwords/current-status")]
        public PasswordStatusResponse GetPasswordStatus()
        {
            return new PasswordStatusResponse
            {
                valid = userSession != null,
            };
        }

        private LoginRequestContext CreateLoginRequestContext()
        {
            return new LoginRequestContext
            {
                hashedIp = GetIP(),
                userAgent = UserAgent,
                isRobloxClient = isRoblox,
                isPasswordLeaked = isPasswordLeaked,
            };
        }
    }
}
