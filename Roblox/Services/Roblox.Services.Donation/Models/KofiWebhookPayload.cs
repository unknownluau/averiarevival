using System.Text.Json.Serialization;

namespace Roblox.Services.Donation.Models;

public sealed class KofiWebhookPayload
{
    [JsonPropertyName("verification_token")]
    public string? VerificationToken { get; set; }

    [JsonPropertyName("message_id")]
    public string? MessageId { get; set; }

    [JsonPropertyName("type")]
    public string? Type { get; set; }

    [JsonPropertyName("from_name")]
    public string? FromName { get; set; }

    [JsonPropertyName("amount")]
    public string? Amount { get; set; }

    [JsonPropertyName("currency")]
    public string? Currency { get; set; }

    [JsonPropertyName("is_subscription_payment")]
    public bool IsSubscriptionPayment { get; set; }
}
