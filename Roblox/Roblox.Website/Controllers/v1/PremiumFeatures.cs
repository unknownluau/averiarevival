using Microsoft.AspNetCore.Mvc;
using Roblox.Exceptions;

namespace Roblox.Website.Controllers;

[ApiController]
[Route("/apisite/premiumfeatures/v1")]
public class PremiumFeaturesControllerV1 : ControllerBase
{
    [HttpGet("users/{userId:long}/subscriptions")]
    public dynamic GetUserSubscription()
    {
        return new
        {
            subscriptionProductModel = new
            {
                premiumFeatureId = 505,
                subscriptionTypeName = "RobloxPremium450",
                robuxStipendAmount = 450,
                isLifetime = true,
                expiration = "2200-02-07T17:00:00.000Z",
                renewal = (object?) null,
                created = "2200-02-07T17:00:00.000Z",
                purchasePlatform = "isDesktop",
            }
        };
    }

    [HttpGet("products")]
    public dynamic GetProducts()
    {
        // todo: maybe?
        // see: services/api/src/services/Billing.ts
        throw new NotImplementedException();
    }

    [HttpGet("users/{userId:long}/validate-membership")]
    public async Task<int> ValidateMembership(long userId)
    {
        var membership = await services.users.GetUserMembership(userId);
        if (membership == null)
            return 0;
        return (int) membership.membershipType;
    }

    [HttpGet("users/{userId:long}/playtime")]
    public async Task<dynamic> GetUserPlaytime(long userId)
    {
        if (userSession == null || userSession.userId != userId)
            throw new HttpException(System.Net.HttpStatusCode.Forbidden);
        var totalSeconds = await services.games.GetTotalPlaytimeSeconds(userId);
        var totalHours = totalSeconds / 3600.0;
        return new
        {
            totalPlaytimeHours = Math.Round(totalHours, 2),
            meetsRequirement = totalHours >= 6.0,
            requiredHours = 6.0,
            remainingHours = Math.Max(0, Math.Round(6.0 - totalHours, 2)),
        };
    }
}