using System.Net;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using Roblox.Dto.Avatar;
using Roblox.Models.Avatar;
using Roblox.Models.Users;
using Roblox.Services;
using StackExchange.Redis;

namespace Roblox.Services.Avatar.Tests;

public sealed class AvatarRouteTestFixture : IAsyncDisposable
{
    private static readonly Lazy<Task<bool>> InfrastructureAvailable = new(CheckInfrastructureAvailableAsync);

    private readonly AvatarServiceApplicationFactory _factory;
    private readonly Dictionary<string, string> _cookies = new(StringComparer.Ordinal);

    private AvatarRouteTestFixture(AvatarServiceApplicationFactory factory, HttpClient client, long userId)
    {
        _factory = factory;
        Client = client;
        UserId = userId;
    }

    public HttpClient Client { get; }
    public long UserId { get; }
    public long OutfitId { get; private set; }

    public static string PostgresConnectionString =>
        Environment.GetEnvironmentVariable("AVERIA_TEST_POSTGRES") ??
        "Host=localhost;Port=5432;Database=roblox_integration_test;Username=roblox_integration_test_user;Password=docker;Timeout=1;Command Timeout=1";

    public static string RedisConnectionString =>
        Environment.GetEnvironmentVariable("AVERIA_TEST_REDIS") ?? "localhost:6379,connectTimeout=500,syncTimeout=500,abortConnect=false";

    public static bool DockerTestsRequired =>
        string.Equals(Environment.GetEnvironmentVariable("AVERIA_REQUIRE_DOCKER_TESTS"), "true", StringComparison.OrdinalIgnoreCase);

    public static string JsonDataDirectory => FindJsonDataDirectory();

    public static async Task<AvatarRouteTestFixture?> CreateAsync()
    {
        if (!await IsInfrastructureAvailableAsync())
        {
            if (DockerTestsRequired)
            {
                throw new InvalidOperationException("Docker-backed Avatar route tests require migrated Postgres and Redis.");
            }

            return null;
        }

        ApplyServiceEnvironment();
        var factory = new AvatarServiceApplicationFactory();
        var client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
        });
        client.Timeout = TimeSpan.FromSeconds(15);

        var userId = await CreateSeedUserAsync();
        return new AvatarRouteTestFixture(factory, client, userId);
    }

    private static void ApplyServiceEnvironment()
    {
        Environment.SetEnvironmentVariable("Postgres", PostgresConnectionString);
        Environment.SetEnvironmentVariable("Redis", RedisConnectionString);
        Environment.SetEnvironmentVariable("Authorization", "AvatarRouteTestAuthorization");
        Environment.SetEnvironmentVariable("RccAuthorization", "AvatarRouteTestRccAuthorization");
        Environment.SetEnvironmentVariable("BaseUrl", "http://localhost");
        Environment.SetEnvironmentVariable("CdnBaseUrl", "http://localhost");
        Environment.SetEnvironmentVariable("IsCdnEnabled", "false");
        Environment.SetEnvironmentVariable("Directories__JsonData", JsonDataDirectory);
    }

    public static async Task<bool> IsInfrastructureAvailableAsync()
    {
        return await InfrastructureAvailable.Value;
    }

    private static async Task<bool> CheckInfrastructureAvailableAsync()
    {
        try
        {
            await using var connection = new NpgsqlConnection(PostgresConnectionString);
            await connection.OpenAsync();
            await using var command = new NpgsqlCommand("select to_regclass('public.user') is not null", connection);
            var hasUserTable = (bool?)await command.ExecuteScalarAsync() == true;
            if (!hasUserTable)
            {
                return false;
            }

            var redis = await ConnectionMultiplexer.ConnectAsync(RedisConnectionString);
            await redis.GetDatabase().PingAsync();
            redis.Dispose();
            return true;
        }
        catch
        {
            return false;
        }
    }

    public void AddCookie(string name, string value)
    {
        _cookies[name] = value;
    }

    public async Task EnsureOutfitAsync()
    {
        using var avatar = ServiceProvider.GetOrCreate<AvatarService>();
        var existingAvatar = await avatar.GetAvatar(UserId);
        var details = new OutfitExtendedDetails
        {
            assetIds = Array.Empty<long>(),
            details = new OutfitAvatar
            {
                userId = UserId,
                headColorId = existingAvatar.headColorId,
                torsoColorId = existingAvatar.torsoColorId,
                leftArmColorId = existingAvatar.leftArmColorId,
                rightArmColorId = existingAvatar.rightArmColorId,
                leftLegColorId = existingAvatar.leftLegColorId,
                rightLegColorId = existingAvatar.rightLegColorId,
                height = existingAvatar.scales.height,
                width = existingAvatar.scales.width,
                head = existingAvatar.scales.head,
                depth = existingAvatar.scales.depth,
                proportion = existingAvatar.scales.proportion,
                bodyType = existingAvatar.scales.bodyType,
                avatarType = existingAvatar.avatarType,
            },
        };

        await avatar.CreateOutfit(UserId, "RouteTest", "headshot-test.png", "thumbnail-test.png", details);
        var outfits = (await avatar.GetUserOutfits(UserId, 1, 0)).ToList();
        OutfitId = outfits[0].id;
    }

    public async Task<HttpResponseMessage> SendAsync(AvatarRouteCase route, bool authenticated)
    {
        if (route.Arrange != null)
        {
            await route.Arrange(this);
        }

        var request = new HttpRequestMessage(
            new HttpMethod(route.Method),
            ResolvePath(route.Path));

        if (route.CreateContent != null)
        {
            request.Content = await route.CreateContent(this);
        }

        if (authenticated)
        {
            AddSessionHeaders(request);
        }

        if (_cookies.Count > 0)
        {
            request.Headers.Add(
                "Cookie",
                string.Join("; ", _cookies.Select(cookie => $"{cookie.Key}={WebUtility.UrlEncode(cookie.Value)}")));
        }

        return await Client.SendAsync(request);
    }

    private static async Task<long> CreateSeedUserAsync()
    {
        var username = "AvatarRoute" + Guid.NewGuid().ToString("N")[..12];
        using var users = ServiceProvider.GetOrCreate<UsersService>();
        var created = await users.CreateUser(username, "password123", Gender.Male);

        using var avatar = ServiceProvider.GetOrCreate<AvatarService>();
        await avatar.UpdateUserAvatarImages(created.userId, "headshot-test.png", "thumbnail-test.png", "thumbnail3d-test.json");

        return created.userId;
    }

    private static string FindJsonDataDirectory()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory != null)
        {
            var candidate = Path.Combine(directory.FullName, "Roblox.Libraries", "Json");
            if (File.Exists(Path.Combine(candidate, "avatar-colors.json")))
            {
                return candidate + Path.DirectorySeparatorChar;
            }

            directory = directory.Parent;
        }

        return Path.Combine(AppContext.BaseDirectory, "Roblox.Libraries", "Json") + Path.DirectorySeparatorChar;
    }

    private string ResolvePath(string path)
    {
        return path
            .Replace("{userId}", UserId.ToString(), StringComparison.Ordinal)
            .Replace("{outfitId}", OutfitId == 0 ? "0" : OutfitId.ToString(), StringComparison.Ordinal)
            .Replace("{assetId}", "0", StringComparison.Ordinal)
            .Replace("{recentType}", "all", StringComparison.Ordinal);
    }

    private void AddSessionHeaders(HttpRequestMessage request)
    {
        request.Headers.Add("X-Averia-UserId", UserId.ToString());
        request.Headers.Add("X-Averia-Username", "AvatarRouteTest");
        request.Headers.Add("X-Averia-SessionId", Guid.NewGuid().ToString("N"));
        request.Headers.Add("X-Averia-AccountStatus", AccountStatus.Ok.ToString());
    }

    public async ValueTask DisposeAsync()
    {
        Client.Dispose();
        await _factory.DisposeAsync();
    }

    private sealed class AvatarServiceApplicationFactory : WebApplicationFactory<Program>
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment("Testing");
            builder.ConfigureServices(services =>
            {
                services.AddExceptionHandler<AvatarRouteTestExceptionHandler>();
            });
            builder.ConfigureAppConfiguration((_, configuration) =>
            {
                configuration.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["Postgres"] = PostgresConnectionString,
                    ["Redis"] = RedisConnectionString,
                    ["Authorization"] = "AvatarRouteTestAuthorization",
                    ["RccAuthorization"] = "AvatarRouteTestRccAuthorization",
                    ["BaseUrl"] = "http://localhost",
                    ["CdnBaseUrl"] = "http://localhost",
                    ["IsCdnEnabled"] = "false",
                    ["Directories:JsonData"] = JsonDataDirectory,
                });
            });
        }
    }

    private sealed class AvatarRouteTestExceptionHandler : IExceptionHandler
    {
        public async ValueTask<bool> TryHandleAsync(
            HttpContext httpContext,
            Exception exception,
            CancellationToken cancellationToken)
        {
            httpContext.Response.StatusCode = 500;
            await httpContext.Response.WriteAsJsonAsync(
                new
                {
                    error = exception.GetType().FullName,
                    message = exception.Message,
                },
                cancellationToken);

            return true;
        }
    }
}
