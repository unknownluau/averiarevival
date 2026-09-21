using System;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Roblox.Services.Donations;

namespace Roblox.Services.Donation.Services;

public sealed class DonationDiscordNotifier(
    IHttpClientFactory httpClientFactory,
    IConfiguration configuration,
    ILogger<DonationDiscordNotifier> logger)
{
    public async Task NotifyAsync(DonationRewardResult result)
    {
        var webhookUrl = configuration["Discord:WebhookUrl"];
        if (string.IsNullOrWhiteSpace(webhookUrl))
            return;

        try
        {
            var title = result.Status == "granted" ? "Donation rewards granted" : "Donation needs review";
            var donor = result.DonorDisplayName ?? "Unknown";
            if (result.UserId.HasValue)
                donor = $"[{donor}](https://www.averia.lol/users/{result.UserId.Value}/profile)";

            var rewardStatus = result.Status == "granted"
                ? $"{result.Tier!.Robux:N0} R$ and {result.Tier.AssetIds.Count} item(s)"
                : $"Skipped: {result.SkipReason}";
            var payload = new
            {
                embeds = new[]
                {
                    new
                    {
                        title,
                        color = result.Status == "granted" ? 0x43B581 : 0xFAA61A,
                        fields = new object[]
                        {
                            new { name = "Provider", value = result.Provider, inline = true },
                            new { name = "Event ID", value = result.ExternalEventId, inline = true },
                            new { name = "Donor", value = donor, inline = false },
                            new { name = "Amount", value = $"{result.Amount:0.00} {result.Currency}", inline = true },
                            new { name = "On-site rewards", value = rewardStatus, inline = false },
                        },
                    },
                },
            };

            using var content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");
            using var response = await httpClientFactory.CreateClient().PostAsync(webhookUrl, content);
            if (!response.IsSuccessStatusCode)
                logger.LogWarning("Discord webhook returned {StatusCode} for donation event {EventId}", response.StatusCode, result.ExternalEventId);
        }
        catch (Exception exception)
        {
            logger.LogWarning(exception, "Discord notification failed for donation event {EventId}", result.ExternalEventId);
        }
    }
}
