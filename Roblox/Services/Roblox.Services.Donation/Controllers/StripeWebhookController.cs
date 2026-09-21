using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Roblox.Services.Donations;
using Roblox.Services.Donation.Services;
using Roblox.Web.Infrastructure.Controllers;
using Roblox.Web.Infrastructure.Metadata;
using Stripe;
using Stripe.Checkout;

namespace Roblox.Services.Donation.Controllers;

[ApiController]
[InternalServiceOnly]
[Route("stripe-api/webhook")]
public sealed class StripeWebhookController(
    IConfiguration configuration,
    DonationDiscordNotifier discordNotifier,
    ILogger<StripeWebhookController> logger) : RobloxControllerBase
{
    [HttpPost]
    [AllowRobloxAnonymous]
    [BrowserFacingEndpoint]
    public async Task<IActionResult> Webhook()
    {
        try
        {
            using var reader = new StreamReader(HttpContext.Request.Body);
            var json = await reader.ReadToEndAsync();
            var stripeEvent = EventUtility.ConstructEvent(
                json,
                Request.Headers["Stripe-Signature"],
                configuration["Stripe:WebhookSecret"]);

            if (stripeEvent.Type != EventTypes.CheckoutSessionCompleted)
                return Ok();

            var session = stripeEvent.Data.Object as Session;
            var customUserId = session?.CustomFields
                .FirstOrDefault(field => field.Label?.Custom == "Averia User ID")
                ?.Text?.Value;
            var parsedUserId = long.TryParse(customUserId, out var userId) ? userId : (long?)null;
            var result = await services.donationRewards.ProcessAsync(new DonationRewardRequest(
                "stripe",
                stripeEvent.Id,
                (session?.AmountTotal ?? 0) / 100m,
                session?.Currency?.ToUpperInvariant() ?? "",
                customUserId,
                parsedUserId,
                parsedUserId.HasValue ? null : "missing-or-invalid-user-id"));

            if (!result.IsDuplicate)
                await discordNotifier.NotifyAsync(result);

            return Ok();
        }
        catch (StripeException exception)
        {
            logger.LogWarning(exception, "Stripe webhook signature validation failed");
            return BadRequest();
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Stripe webhook processing failed");
            return StatusCode(StatusCodes.Status500InternalServerError);
        }
    }
}
