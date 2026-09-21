using System.Net;
using System.Net.Http.Headers;
using DSharpPlus.Entities;
using Newtonsoft.Json;
using Roblox.Logging;
using System.Text;
namespace Roblox.Libraries.DiscordApi;


public class DiscordBotApi
{
    private readonly HttpClient discordClient;

    public DiscordBotApi (string token)
        : this(new HttpClient(new HttpClientHandler { AutomaticDecompression = DecompressionMethods.All }), token)
    {
    }

    public DiscordBotApi(HttpClient client, string token)
    {
        discordClient = client;
        discordClient.BaseAddress ??= new Uri("https://discord.com/api/");
        discordClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bot", token);
    }

    public async Task AddGuildMember(string guildId, string discordId, string accessToken)
    {
        var data = new Dictionary<string,string>
        {
            {"access_token", accessToken},
        };
        var jsonData = JsonConvert.SerializeObject(data);
        var contentData = new StringContent(jsonData, Encoding.UTF8, "application/json");
        var result = await discordClient.PutAsync($"guilds/{guildId}/members/{discordId}", contentData);
        if (result.IsSuccessStatusCode)
        {
            Writer.Info(LogGroup.DiscordApi, "Succcessfully added {0} to Averia", discordId);
        }
        else
        {
            Writer.Info(LogGroup.DiscordApi, "Failed to add {0} to averia status: {1} with response: {2}", discordId, result.StatusCode, await result.Content.ReadAsStringAsync());
        }
    }
    public async Task<bool> MessageUser(string discordId, string content, DiscordEmbed? discordEmbed = null)
    {
        var channel = await GetDMChannel(discordId);
        if (channel == null)
        {
            Writer.Info(LogGroup.DiscordApi, "Failed to get DM channel for {0}", discordId);
            return false;
        }

        return await SendMessageInChannel(channel.Id.ToString(), content, discordEmbed);
    }

    public async Task<bool> SendMessageInChannel(string channelId, string content, DiscordEmbed? discordEmbed = null)
    {
        var data = new Dictionary<string, dynamic>
        {
            {"content", content}
        };

        if (discordEmbed != null)
        {
            data["embeds"] = new List<DiscordEmbed?> { discordEmbed };
        }
        var jsonData = JsonConvert.SerializeObject(data);
        var contentData = new StringContent(jsonData, Encoding.UTF8, "application/json");
        var result = await discordClient.PostAsync($"channels/{channelId}/messages", contentData);
        if (result.IsSuccessStatusCode)
        {
            Writer.Info(LogGroup.DiscordApi, "Succcessfully messaged {0} to Averia", channelId);
            return true;
        }

        Writer.Info(LogGroup.DiscordApi, "Failed to message {0} to averia status: {1}", channelId, result.StatusCode);
        return false;
    }

    public async Task<bool> BanGuildMember(string guildId, string discordId, string auditReason)
    {
        using var request = new HttpRequestMessage(
            HttpMethod.Put,
            $"guilds/{Uri.EscapeDataString(guildId)}/bans/{Uri.EscapeDataString(discordId)}");
        request.Headers.TryAddWithoutValidation("X-Audit-Log-Reason", Uri.EscapeDataString(auditReason));
        request.Content = new StringContent("{\"delete_message_seconds\":0}", Encoding.UTF8, "application/json");

        using var response = await discordClient.SendAsync(request);
        if (response.IsSuccessStatusCode)
        {
            Writer.Info(LogGroup.DiscordApi, "Successfully banned Discord user {0} from guild {1}", discordId, guildId);
            return true;
        }

        Writer.Info(
            LogGroup.DiscordApi,
            "Failed to ban Discord user {0} from guild {1}. Status: {2}. Response: {3}",
            discordId,
            guildId,
            response.StatusCode,
            await response.Content.ReadAsStringAsync());
        return false;
    }

    private async Task<DiscordDmChannel?> GetDMChannel(string discordId)
    {
        var data = new Dictionary<string,string>
        {
            {"recipient_id", discordId},
        };
        var jsonData = JsonConvert.SerializeObject(data);
        var contentData = new StringContent(jsonData, Encoding.UTF8, "application/json");
        var result = await discordClient.PostAsync($"users/@me/channels", contentData);
        if (!result.IsSuccessStatusCode)
        {
            Writer.Info(LogGroup.DiscordApi, "Failed to create DM channel for {0} status: {1}", discordId, result.StatusCode);
            return null;
        }

        var json = await result.Content.ReadAsStringAsync();
        var channel = JsonConvert.DeserializeObject<DiscordDmChannel>(json);
        return channel;
    }
}
