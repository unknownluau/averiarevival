using Microsoft.Extensions.Configuration;
using Roblox.Rendering;

namespace Roblox.ServiceDefaults;

public static class RobloxServiceInfrastructure
{
    private static int _initialized;

    public static void Initialize(IConfiguration configuration)
    {
        if (Interlocked.Exchange(ref _initialized, 1) == 1)
        {
            return;
        }

        var postgres = configuration.GetSection("Postgres").Value;
        if (!string.IsNullOrWhiteSpace(postgres))
        {
            Roblox.Services.Database.Configure(postgres);
        }

        var redis = configuration.GetSection("Redis").Value;
        if (!string.IsNullOrWhiteSpace(redis))
        {
            Roblox.Services.Cache.Configure(
                redis,
                configuration.GetSection("RedisAuthentication").Value);
        }

        Roblox.Configuration.CdnBaseUrl = configuration["CdnBaseUrl"] ?? Roblox.Configuration.CdnBaseUrl ?? string.Empty;
        Roblox.Configuration.IsCdnEnabled = configuration.GetValue("IsCdnEnabled", Roblox.Configuration.IsCdnEnabled);
        Roblox.Configuration.BaseUrl = configuration["BaseUrl"] ?? Roblox.Configuration.BaseUrl ?? string.Empty;
        Roblox.Configuration.ShortBaseUrl = !string.IsNullOrWhiteSpace(Roblox.Configuration.BaseUrl)
            ? Roblox.Configuration.BaseUrl.Replace("https", "http").Replace("http://www.", "")
            : Roblox.Configuration.ShortBaseUrl ?? string.Empty;

        Roblox.Configuration.AssetDirectory = configuration["Directories:Asset"] ?? Roblox.Configuration.AssetDirectory ?? string.Empty;
        Roblox.Configuration.StorageDirectory = configuration["Directories:Storage"] ?? Roblox.Configuration.StorageDirectory ?? string.Empty;
        Roblox.Configuration.ThumbnailsDirectory = configuration["Directories:Thumbnails"] ?? Roblox.Configuration.ThumbnailsDirectory ?? string.Empty;
        Roblox.Configuration.GroupIconsDirectory = configuration["Directories:GroupIcons"] ?? Roblox.Configuration.GroupIconsDirectory ?? string.Empty;
        Roblox.Configuration.PublicDirectory = configuration["Directories:Public"] ?? Roblox.Configuration.PublicDirectory ?? string.Empty;
        Roblox.Configuration.XmlTemplatesDirectory = configuration["Directories:XmlTemplates"] ?? Roblox.Configuration.XmlTemplatesDirectory ?? string.Empty;
        Roblox.Configuration.JsonDataDirectory = configuration["Directories:JsonData"] ?? Roblox.Configuration.JsonDataDirectory ?? string.Empty;
        Roblox.Configuration.ScriptDirectory = configuration["Directories:ScriptsData"] ?? Roblox.Configuration.ScriptDirectory ?? string.Empty;
        Roblox.Configuration.AdminBundleDirectory = configuration["Directories:AdminBundle"] ?? Roblox.Configuration.AdminBundleDirectory ?? string.Empty;
        Roblox.Configuration.EconomyChatBundleDirectory = configuration["Directories:EconomyChatBundle"] ?? Roblox.Configuration.EconomyChatBundleDirectory ?? string.Empty;
        Roblox.Configuration.LuaScriptsDirectory = configuration["Directories:RCCLuaScripts"] ?? Roblox.Configuration.LuaScriptsDirectory ?? string.Empty;

        Roblox.Configuration.HmacSecret = configuration["HmacSecret"] ?? Roblox.Configuration.HmacSecret ?? string.Empty;
        Roblox.Configuration.R2AccountId = configuration["CloudflareR2:AccountId"] ?? Roblox.Configuration.R2AccountId ?? string.Empty;
        Roblox.Configuration.R2AccessKey = configuration["CloudflareR2:AccessKey"] ?? Roblox.Configuration.R2AccessKey ?? string.Empty;
        Roblox.Configuration.R2SecretKey = configuration["CloudflareR2:SecretKey"] ?? Roblox.Configuration.R2SecretKey ?? string.Empty;
        Roblox.Configuration.R2BucketName = configuration["CloudflareR2:BucketName"] ?? Roblox.Configuration.R2BucketName ?? string.Empty;

        Roblox.Configuration.DiscordBotToken = configuration["Discord:BotToken"] ?? Roblox.Configuration.DiscordBotToken ?? string.Empty;
        Roblox.Configuration.DiscordGuildId = configuration["Discord:GuildId"] ?? Roblox.Configuration.DiscordGuildId ?? string.Empty;
        Roblox.Configuration.DiscordLogChannelId = configuration["Discord:LogChannelId"] ?? Roblox.Configuration.DiscordLogChannelId ?? string.Empty;
    
        Roblox.Configuration.ArbiterAuthorization = configuration["ArbiterAuthorization"] ?? Roblox.Configuration.ArbiterAuthorization ?? string.Empty;
        
        var renderBaseUrl = configuration["Render:BaseUrl"] ?? $"https://arbiter.{Roblox.Configuration.ShortBaseUrl}";
        if (!string.IsNullOrWhiteSpace(renderBaseUrl))
        {
            RenderHttpClient.Configure(
                renderBaseUrl,
                Roblox.Configuration.ArbiterAuthorization,
                configuration.GetValue("Render:UseBinaryTransport", true));
        }
    }
}
