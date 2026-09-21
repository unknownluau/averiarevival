using System;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Roblox.Services.Donation.Services;
using Roblox.Services.Donations;
using Roblox.Web.Infrastructure.Controllers;
using Roblox.Web.Infrastructure.Metadata;

namespace Roblox.Services.Donation.Controllers;

[ApiController]
[InternalServiceOnly]
[Route("donation-api/rewarble")]
public sealed class RewarbleController(
    IConfiguration configuration,
    IHttpClientFactory httpClientFactory,
    DonationDiscordNotifier discordNotifier,
    ILogger<RewarbleController> logger) : RobloxControllerBase
{
    private const int MinimumVoucherLength = 16;

    [HttpPost("redeem")]
    [RequireRobloxSession]
    [BrowserFacingEndpoint]
    public async Task<IActionResult> Redeem([FromBody] RewarbleRedeemRequest request, CancellationToken cancellationToken)
    {
        var code = (request.Code ?? "").Trim().ToUpperInvariant();
        if (code.Length < MinimumVoucherLength)
            return RewarbleError(StatusCodes.Status400BadRequest, "VOUCHER_INVALID_LENGTH");

        var apiKey = configuration["Rewarble:ApiKey"];
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            logger.LogError("Rewarble API key is not configured");
            return RewarbleError(StatusCodes.Status500InternalServerError, "REWARBLE_NOT_CONFIGURED");
        }

        RewarbleRedeemResponse rewarbleResponse;
        try
        {
            var response = await SendRewarbleRedeemRequest(code, apiKey, cancellationToken);
            if (response.StatusCode != HttpStatusCode.OK)
            {
                if (response.StatusCode == HttpStatusCode.InternalServerError)
                    return RewarbleError(StatusCodes.Status502BadGateway, "REWARBLE_UNAVAILABLE");

                var errorCode = (await response.Content.ReadAsStringAsync(cancellationToken)).Trim();
                if (string.IsNullOrWhiteSpace(errorCode))
                    errorCode = "REWARBLE_REDEEM_FAILED";

                return RewarbleError((int)response.StatusCode, errorCode);
            }

            var payload = await response.Content.ReadAsStringAsync(cancellationToken);
            rewarbleResponse = JsonSerializer.Deserialize<RewarbleRedeemResponse>(payload, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true,
            }) ?? throw new JsonException("Empty Rewarble success response");
        }
        catch (JsonException exception)
        {
            logger.LogWarning(exception, "Rewarble redeem response could not be parsed");
            return RewarbleError(StatusCodes.Status502BadGateway, "REWARBLE_BAD_RESPONSE");
        }
        catch (HttpRequestException exception)
        {
            logger.LogWarning(exception, "Rewarble redeem request failed");
            return RewarbleError(StatusCodes.Status502BadGateway, "REWARBLE_UNAVAILABLE");
        }
        catch (TaskCanceledException exception) when (!cancellationToken.IsCancellationRequested)
        {
            logger.LogWarning(exception, "Rewarble redeem request timed out");
            return RewarbleError(StatusCodes.Status504GatewayTimeout, "REWARBLE_TIMEOUT");
        }

        if (string.IsNullOrWhiteSpace(rewarbleResponse.TransactionWTRX))
        {
            logger.LogWarning("Rewarble redeem response did not include transactionWTRX");
            return RewarbleError(StatusCodes.Status502BadGateway, "REWARBLE_BAD_RESPONSE");
        }

        var currency = rewarbleResponse.FaceValueCurrency?.ToUpperInvariant() ?? "";
        var rewardResult = await services.donationRewards.ProcessAsync(new DonationRewardRequest(
            "rewarble",
            rewarbleResponse.TransactionWTRX,
            rewarbleResponse.FaceValue,
            currency,
            safeUserSession.username,
            safeUserSession.userId));

        if (!rewardResult.IsDuplicate)
            await discordNotifier.NotifyAsync(rewardResult);

        return Ok(new
        {
            success = rewardResult.Status == "granted",
            status = rewardResult.Status,
            message = GetResultMessage(rewardResult),
            voucher = new
            {
                faceValue = rewarbleResponse.FaceValue,
                faceValueCurrency = currency,
                voucherSerial = rewarbleResponse.VoucherSerial,
                transactionWTRX = rewarbleResponse.TransactionWTRX,
                state = rewarbleResponse.State,
            },
            reward = rewardResult.Tier == null
                ? null
                : new
                {
                    robux = rewardResult.Tier.Robux,
                    assetIds = rewardResult.Tier.AssetIds,
                },
        });
    }

    private async Task<HttpResponseMessage> SendRewarbleRedeemRequest(
        string code,
        string apiKey,
        CancellationToken cancellationToken)
    {
        var baseUrl = configuration["Rewarble:BaseUrl"];
        if (string.IsNullOrWhiteSpace(baseUrl))
            baseUrl = "https://api.rewarble.com";

        var redeemUrl = $"{baseUrl.TrimEnd('/')}/client/1.00/redeem";
        if (bool.TryParse(configuration["Rewarble:TestMode"], out var testMode) && testMode)
            redeemUrl += "?test=true";

        using var httpRequest = new HttpRequestMessage(HttpMethod.Post, redeemUrl);
        httpRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
        httpRequest.Content = JsonContent.Create(new { code });

        var client = httpClientFactory.CreateClient();
        client.Timeout = TimeSpan.FromSeconds(15);
        return await client.SendAsync(httpRequest, cancellationToken);
    }

    private ObjectResult RewarbleError(int statusCode, string code)
    {
        var message = GetErrorMessage(code);
        return StatusCode(statusCode, new
        {
            success = false,
            code,
            message,
            errors = new[]
            {
                new
                {
                    code,
                    message,
                },
            },
        });
    }

    private static string GetErrorMessage(string code)
    {
        return code switch
        {
            "VOUCHER_INVALID_LENGTH" or "VOUCHER_INVALID_LENGHT" => "Voucher code must be at least 16 characters long.",
            "VOUCHER_EXPIRED" => "This voucher has expired.",
            "UNKNOWN_VOUCHER" => "This voucher could not be found.",
            "VOUCHER_USED" => "This voucher has already been used.",
            "VOUCHER_INACTIVE" => "This voucher is inactive.",
            "VOUCHER_EMPTY" => "This voucher has no balance on it.",
            "REWARBLE_NOT_CONFIGURED" => "Voucher redemption is not configured yet.",
            "REWARBLE_UNAVAILABLE" => "Rewarble is temporarily unavailable. Try again shortly.",
            "REWARBLE_TIMEOUT" => "Rewarble took too long to respond. Try again shortly.",
            "REWARBLE_BAD_RESPONSE" => "Rewarble returned an unexpected response. Please contact support at https://support.averia.one/submit-request",
            _ => "This voucher could not be redeemed.",
        };
    }

    private static string GetResultMessage(DonationRewardResult result)
    {
        if (result.IsDuplicate)
            return "This voucher has already been processed on Averia.";

        return result.Status switch
        {
            "granted" => "Rewards granted.",
            "skipped" => "Voucher redeemed, but rewards need manual review. Please contact support at https://support.averia.one/submit-request",
            _ => "Voucher redeemed, but rewards need manual review. Please contact support at https://support.averia.one/submit-request",
        };
    }
}

public sealed record RewarbleRedeemRequest([property: JsonPropertyName("code")] string? Code);

public sealed record RewarbleRedeemResponse(
    [property: JsonPropertyName("faceValue")] decimal FaceValue,
    [property: JsonPropertyName("faceValueCurrency")] string? FaceValueCurrency,
    [property: JsonPropertyName("voucherSerial")] string? VoucherSerial,
    [property: JsonPropertyName("transactionWTRX")] string? TransactionWTRX,
    [property: JsonPropertyName("state")] string? State);
