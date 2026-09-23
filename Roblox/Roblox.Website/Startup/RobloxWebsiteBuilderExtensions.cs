using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Mvc.Formatters;
using Microsoft.OpenApi;
using Roblox;
using Roblox.Dto.Users;
using Roblox.Rendering;
using Roblox.Services;
using Roblox.Services.App.FeatureFlags;
using Roblox.ServiceDefaults;
using Roblox.Web.Infrastructure.Extensions;
using Roblox.Website.ExceptionHandlers;
using Roblox.Website.HostedServices;
using Roblox.Website.Middleware;

namespace Roblox.Website.Startup;

public static class RobloxWebsiteBuilderExtensions
{
    public static void InitializeLegacyConfiguration(this WebApplicationBuilder builder)
    {
        var configuration = builder.Configuration;
        var options = configuration.Get<RobloxWebsiteOptions>() ?? new RobloxWebsiteOptions();
        builder.Logging.AddFilter("Microsoft.AspNetCore.Diagnostics.ExceptionHandlerMiddleware", LogLevel.None);
        builder.Services.Configure<RobloxWebsiteOptions>(configuration);

        Roblox.Services.Database.Configure(options.Postgres);
        Roblox.Services.Cache.Configure(options.Redis, options.RedisAuthentication);

        Roblox.Configuration.CdnBaseUrl = options.CdnBaseUrl;
        Roblox.Configuration.AssetDirectory = options.Directories.Asset;
        Roblox.Configuration.StorageDirectory = options.Directories.Storage;
        Roblox.Configuration.ThumbnailsDirectory = options.Directories.Thumbnails;
        Roblox.Configuration.GroupIconsDirectory = options.Directories.GroupIcons;
        Roblox.Configuration.PublicDirectory = options.Directories.Public;
        Roblox.Configuration.XmlTemplatesDirectory = options.Directories.XmlTemplates;
        Roblox.Configuration.JsonDataDirectory = options.Directories.JsonData;
        Roblox.Configuration.ScriptDirectory = options.Directories.ScriptsData;
        Roblox.Configuration.AdminBundleDirectory = options.Directories.AdminBundle;
        Roblox.Configuration.EconomyChatBundleDirectory = options.Directories.EconomyChatBundle;
        Roblox.Configuration.BaseUrl = options.BaseUrl;
        Roblox.Configuration.ShortBaseUrl = options.BaseUrl.Replace("https", "http").Replace("http://www.", "");
        Roblox.Configuration.HCaptchaPublicKey = options.HCaptcha.Public;
        Roblox.Configuration.HCaptchaPrivateKey = options.HCaptcha.Private;
        Roblox.Configuration.IsCdnEnabled = options.IsCdnEnabled;
        Roblox.Configuration.HmacSecret = options.HmacSecret;
        Roblox.Configuration.R2AccountId = options.CloudflareR2.AccountId;
        Roblox.Configuration.R2AccessKey = options.CloudflareR2.AccessKey;
        Roblox.Configuration.R2SecretKey = options.CloudflareR2.SecretKey;
        Roblox.Configuration.R2BucketName = options.CloudflareR2.BucketName;
        Roblox.Configuration.RobloxClientId = options.Roblox.ClientId;
        Roblox.Configuration.RobloxClientSecret = options.Roblox.ClientSecret;
        Roblox.Configuration.DiscordClientId = options.Discord.ClientId;
        Roblox.Configuration.DiscordClientSecret = options.Discord.ClientSecret;
        Roblox.Configuration.DiscordGuildId = options.Discord.GuildId;
        Roblox.Configuration.DiscordBotToken = options.Discord.BotToken;
        Roblox.Configuration.DiscordLogChannelId = options.Discord.LogChannelId;
        Roblox.Configuration.DiscordLockChannelId = options.Discord.LockChannelId;
        Roblox.Configuration.DiscordApplicationCallback = options.BaseUrl + options.Discord.ApplicationCallback;
        Roblox.Configuration.DiscordLoginCallback = options.BaseUrl + options.Discord.LoginCallback;
        Roblox.Configuration.DiscordLinkCallback = options.BaseUrl + options.Discord.LinkCallback;
        Roblox.Configuration.LeakCheckApiKey = options.LeakCheckApiKey;
        Roblox.Configuration.GameServerAuthorization = options.GameServerAuthorization;
        Roblox.Configuration.BotAuthorization = options.BotAuthorization;
        Roblox.Configuration.RccAuthorization = options.RccAuthorization;
        Roblox.Configuration.RobloxAuthorization = options.RobloxAuthorization;
        Roblox.Configuration.ArbiterAuthorization = options.ArbiterAuthorization;
        Roblox.Configuration.GameServerIp = options.GameServerIp;
        Roblox.Configuration.UserAgentBypassSecret = options.UserAgentBypassSecret;
        Roblox.Configuration.InvisibleTurnstileSiteKey = options.InvisibleTurnstile.SiteKey;
        Roblox.Configuration.InvisibleTurnstileSecretKey = options.InvisibleTurnstile.SecretKey;
        Roblox.Configuration.OpenRouterApiKey = options.AI.OpenRouterAPIKey;
        Roblox.Configuration.VerificationSecret = options.VerificationSecret;
        Roblox.Configuration.LuaScriptsDirectory = options.Directories.RCCLuaScripts;

        var gameServerConfig = new ConfigurationBuilder()
            .SetBasePath(AppContext.BaseDirectory)
            .AddJsonFile("game-servers.json", optional: true)
            .AddEnvironmentVariables()
            .Build();
        Roblox.Configuration.GameServerIpAddresses = gameServerConfig.GetSection("GameServers").Get<IEnumerable<GameServerConfigEntry>>() ?? Enumerable.Empty<GameServerConfigEntry>();

        Roblox.Configuration.AssetValidationServiceUrl = options.AssetValidation.BaseUrl;
        Roblox.Configuration.AssetValidationServiceAuthorization = options.AssetValidation.Authorization;
        GameServerService.Configure(string.Join(Guid.NewGuid().ToString(), new int[16].Select(_ => Guid.NewGuid().ToString())));
        Roblox.Configuration.PackageShirtAssetId = options.PackageShirtAssetId;
        Roblox.Configuration.PackagePantsAssetId = options.PackagePantsAssetId;
        Roblox.Libraries.TwitterApi.TwitterApi.Configure(options.Twitter.Bearer);
        Roblox.Configuration.SignupAssetIds = options.SignupAssetIds;
        Roblox.Configuration.SignupAvatarAssetIds = options.SignupAvatarAssetIds;

#if DEBUG
        Roblox.Configuration.RobloxAppPrefix = "rbxeconsimdev:";
#endif

        Roblox.Website.Filters.StaffFilter.Configure(options.OwnerUserId);

        ApplicationGuardMiddleware.Configure(options.Authorization);
        CsrfMiddleware.Configure(Guid.NewGuid() + Guid.NewGuid().ToString() + Guid.NewGuid());
        SessionMiddleware.Configure(options.Jwt.Sessions);
        var arbiterUrl = string.IsNullOrWhiteSpace(options.Render.BaseUrl)
            ? $"https://arbiter.{Roblox.Configuration.ShortBaseUrl}/"
            : options.Render.BaseUrl;
        CommandHandler.Configure(arbiterUrl, options.ArbiterAuthorization, options.Render.UseBinaryTransport);
        Roblox.Services.Signer.SignService.Setup();

        RenderingHandler.Configure(arbiterUrl, options.ArbiterAuthorization, options.Render.UseBinaryTransport);
        Roblox.Libraries.RemoteView.RemoteView.Configure("http://localhost:3000", options.Authorization);
    }

    public static IServiceCollection AddRobloxWebsiteServices(this IServiceCollection services, IConfiguration configuration, IHostEnvironment environment)
    {
        Roblox.Services.Assets.AssetRenderQueue.Configure(configuration);
        services.AddRobloxTelemetry(configuration, "Roblox.Website", environment.EnvironmentName);
        services.AddRazorPages();
        services.AddRobloxWebInfrastructure(configuration);
        services.AddSingleton<Roblox.EconomyChat.ChatService>();
        services.AddRequestDecompression();
        services.AddHttpClient("FrontendProxy", client =>
        {
            client.BaseAddress = new Uri("http://localhost:3000");
            client.Timeout = TimeSpan.FromSeconds(20);
            client.DefaultRequestHeaders.ConnectionClose = false;
        })
        .ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler
        {
            AllowAutoRedirect = false,
            MaxConnectionsPerServer = 100,
            UseProxy = false,
            UseCookies = false
        })
        .SetHandlerLifetime(Timeout.InfiniteTimeSpan);
        services.AddControllers(options =>
            {
                options.InputFormatters.Add(new XmlSerializerInputFormatter(options));
                options.RespectBrowserAcceptHeader = true;
            })
            .AddJsonOptions(o =>
            {
                o.JsonSerializerOptions.Converters.Add(new System.Text.Json.Serialization.JsonStringEnumConverter());
                o.JsonSerializerOptions.PropertyNamingPolicy = null;
            });

        services.Configure<FormOptions>(options =>
        {
            options.ValueLengthLimit = int.MaxValue;
            options.MultipartBodyLengthLimit = long.MaxValue;
        });

        services.AddSignalR();
        services.AddEndpointsApiExplorer();
        services.AddSwaggerGen(c =>
        {
            c.ResolveConflictingActions(apiDescriptions => apiDescriptions.First());
            c.IgnoreObsoleteActions();
            c.IgnoreObsoleteProperties();
            c.CustomSchemaIds(type => type.FullName);
            c.EnableAnnotations();
            c.SwaggerDoc("UserV1", new OpenApiInfo
            {
                Version = "v1",
                Title = "Users Api v1",
            });
            c.SchemaGeneratorOptions.SchemaIdSelector = type => type.ToString();
            c.OperationFilter<SwaggerFileOperationFilter>();
            var xmlFilename = $"{System.Reflection.Assembly.GetExecutingAssembly().GetName().Name}.xml";
            c.IncludeXmlComments(Path.Combine(AppContext.BaseDirectory, xmlFilename));
        });
        services.AddMvc(c => c.Conventions.Add(new ApiExplorerGetsOnlyConvention()));

        services.AddExceptionHandler<GlobalExceptionHandler>();
        services.AddProblemDetails();

        services.AddHostedService<FeatureFlagRefreshHostedService>();
        services.AddHostedService<AvatarThumbnailCleanupHostedService>();
        services.AddHostedService<Roblox.Services.Assets.AssetRenderQueueWorker>();
        services.AddSingleton<MachineBanEnforcementSignal>();
        services.AddHostedService<MachineBanEnforcementHostedService>();

        return services;
    }
}
