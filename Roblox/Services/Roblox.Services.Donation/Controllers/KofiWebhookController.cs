using System;
using System.Globalization;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Roblox.Services.Donation.Models;
using Roblox.Services.Donation.Services;
using Roblox.Services.Donations;
using Roblox.Services.Exceptions;
using Roblox.Web.Infrastructure.Controllers;
using Roblox.Web.Infrastructure.Metadata;

namespace Roblox.Services.Donation.Controllers;

[ApiController]
[InternalServiceOnly]
[Route("donation-api/kofi/webhook")]
public sealed class KofiWebhookController(
    IConfiguration configuration,
    DonationDiscordNotifier discordNotifier,
    ILogger<KofiWebhookController> logger) : RobloxControllerBase
{
    [HttpPost]
    [AllowRobloxAnonymous]
    [BrowserFacingEndpoint]
    public async Task<IActionResult> Webhook()
    {
        try
        {
            if (!Request.HasFormContentType)
                return BadRequest();

            var form = await Request.ReadFormAsync();
            var data = form["data"].FirstOrDefault();
            if (string.IsNullOrWhiteSpace(data))
                return BadRequest();

            var payload = JsonSerializer.Deserialize<KofiWebhookPayload>(data);
            if (payload == null)
                return BadRequest();

            var configuredToken = configuration["Kofi:VerificationToken"];
            if (string.IsNullOrWhiteSpace(configuredToken))
            {
                logger.LogError("Ko-fi verification token is not configured");
                return StatusCode(StatusCodes.Status500InternalServerError);
            }

            if (!SecureEquals(configuredToken, payload.VerificationToken ?? ""))
                return Unauthorized();

            if (string.IsNullOrWhiteSpace(payload.MessageId))
                return BadRequest();

            if (!string.Equals(payload.Type, "Donation", StringComparison.OrdinalIgnoreCase) || payload.IsSubscriptionPayment)
                return Ok();

            if (!decimal.TryParse(payload.Amount, NumberStyles.Number, CultureInfo.InvariantCulture, out var amount))
                return BadRequest();

            var donorDisplayName = payload.FromName?.Trim() ?? "";
            long? userId = null;
            string? skipReason = null;
            if (string.IsNullOrWhiteSpace(donorDisplayName))
            {
                skipReason = "missing-display-name";
            }
            else
            {
                try
                {
                    userId = await services.users.GetUserIdFromUsername(donorDisplayName);
                }
                catch (RecordNotFoundException)
                {
                    skipReason = "username-not-found";
                }
            }

            var result = await services.donationRewards.ProcessAsync(new DonationRewardRequest(
                "kofi",
                payload.MessageId,
                amount,
                payload.Currency?.ToUpperInvariant() ?? "",
                donorDisplayName,
                userId,
                skipReason));

            if (!result.IsDuplicate)
                await discordNotifier.NotifyAsync(result);

            return Ok();
        }
        catch (JsonException)
        {
            return BadRequest();
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Ko-fi webhook processing failed");
            return StatusCode(StatusCodes.Status500InternalServerError);
        }
    }

    private static bool SecureEquals(string expected, string actual)
    {
        var expectedBytes = Encoding.UTF8.GetBytes(expected);
        var actualBytes = Encoding.UTF8.GetBytes(actual);
        return expectedBytes.Length == actualBytes.Length
               && CryptographicOperations.FixedTimeEquals(expectedBytes, actualBytes);
    }
}
