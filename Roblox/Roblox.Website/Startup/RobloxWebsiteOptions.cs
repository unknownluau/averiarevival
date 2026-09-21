namespace Roblox.Website.Startup;

public sealed class RobloxWebsiteOptions
{
    public string Postgres { get; set; } = string.Empty;
    public string Redis { get; set; } = string.Empty;
    public string? RedisAuthentication { get; set; }
    public string CdnBaseUrl { get; set; } = string.Empty;
    public RobloxDirectoryOptions Directories { get; set; } = new();
    public string BaseUrl { get; set; } = string.Empty;
    public RobloxHCaptchaOptions HCaptcha { get; set; } = new();
    public bool IsCdnEnabled { get; set; }
    public string HmacSecret { get; set; } = string.Empty;
    public RobloxCloudflareR2Options CloudflareR2 { get; set; } = new();
    public RobloxClientOptions Roblox { get; set; } = new();
    public RobloxDiscordOptions Discord { get; set; } = new();
    public string LeakCheckApiKey { get; set; } = string.Empty;
    public string GameServerAuthorization { get; set; } = string.Empty;
    public string BotAuthorization { get; set; } = string.Empty;
    public string RccAuthorization { get; set; } = string.Empty;
    public string RobloxAuthorization { get; set; } = string.Empty;
    public string ArbiterAuthorization { get; set; } = string.Empty;
    public string GameServerIp { get; set; } = string.Empty;
    public string UserAgentBypassSecret { get; set; } = string.Empty;
    public RobloxInvisibleTurnstileOptions InvisibleTurnstile { get; set; } = new();
    public RobloxAiOptions AI { get; set; } = new();
    public string VerificationSecret { get; set; } = string.Empty;
    public RobloxAssetValidationOptions AssetValidation { get; set; } = new();
    public long PackageShirtAssetId { get; set; }
    public long PackagePantsAssetId { get; set; }
    public List<long> SignupAssetIds { get; set; } = [];
    public List<long> SignupAvatarAssetIds { get; set; } = [];
    public List<long> OwnerUserId { get; set; } = [];
    public string Authorization { get; set; } = string.Empty;
    public RobloxJwtOptions Jwt { get; set; } = new();
    public RobloxRenderOptions Render { get; set; } = new();
    public RobloxTwitterOptions Twitter { get; set; } = new();
}

public sealed class RobloxDirectoryOptions
{
    public string Asset { get; set; } = string.Empty;
    public string Storage { get; set; } = string.Empty;
    public string Thumbnails { get; set; } = string.Empty;
    public string GroupIcons { get; set; } = string.Empty;
    public string Public { get; set; } = string.Empty;
    public string XmlTemplates { get; set; } = string.Empty;
    public string JsonData { get; set; } = string.Empty;
    public string ScriptsData { get; set; } = string.Empty;
    public string AdminBundle { get; set; } = string.Empty;
    public string EconomyChatBundle { get; set; } = string.Empty;
    public string RCCLuaScripts { get; set; } = string.Empty;
}

public sealed class RobloxHCaptchaOptions
{
    public string Public { get; set; } = string.Empty;
    public string Private { get; set; } = string.Empty;
}

public sealed class RobloxCloudflareR2Options
{
    public string AccountId { get; set; } = string.Empty;
    public string AccessKey { get; set; } = string.Empty;
    public string SecretKey { get; set; } = string.Empty;
    public string BucketName { get; set; } = string.Empty;
}

public sealed class RobloxClientOptions
{
    public string ClientId { get; set; } = string.Empty;
    public string ClientSecret { get; set; } = string.Empty;
}

public sealed class RobloxDiscordOptions
{
    public string ClientId { get; set; } = string.Empty;
    public string ClientSecret { get; set; } = string.Empty;
    public string GuildId { get; set; } = string.Empty;
    public string BotToken { get; set; } = string.Empty;
    public string LogChannelId { get; set; } = string.Empty;
    public string LockChannelId { get; set; } = string.Empty;
    public string ApplicationCallback { get; set; } = string.Empty;
    public string LoginCallback { get; set; } = string.Empty;
    public string LinkCallback { get; set; } = string.Empty;
}

public sealed class RobloxInvisibleTurnstileOptions
{
    public string SiteKey { get; set; } = string.Empty;
    public string SecretKey { get; set; } = string.Empty;
}

public sealed class RobloxAiOptions
{
    public string OpenRouterAPIKey { get; set; } = string.Empty;
}

public sealed class RobloxAssetValidationOptions
{
    public string BaseUrl { get; set; } = string.Empty;
    public string Authorization { get; set; } = string.Empty;
}

public sealed class RobloxJwtOptions
{
    public string Sessions { get; set; } = string.Empty;
}

public sealed class RobloxRenderOptions
{
    public string BaseUrl { get; set; } = string.Empty;
    public bool UseBinaryTransport { get; set; } = true;
    public bool UseDurableAssetQueue { get; set; } = true;
}

public sealed class RobloxTwitterOptions
{
    public string Bearer { get; set; } = string.Empty;
}
