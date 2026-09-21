using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using Roblox.Dto.Users;
using Roblox.Models.Users;
using Roblox.Services.App.FeatureFlags;
using StackExchange.Redis;

namespace Roblox.Services.Api.Tests;

public sealed class ApiRouteTestFixture : IAsyncDisposable
{
    private static readonly Lazy<Task<bool>> InfrastructureAvailable = new(CheckInfrastructureAvailableAsync);

    private readonly ApiServiceApplicationFactory _factory;

    private ApiRouteTestFixture(ApiServiceApplicationFactory factory, HttpClient client)
    {
        _factory = factory;
        Client = client;
    }

    public HttpClient Client { get; }

    public static string PostgresConnectionString =>
        Environment.GetEnvironmentVariable("AVERIA_TEST_POSTGRES") ??
        "Host=localhost;Port=5432;Database=roblox_integration_test;Username=roblox_integration_test_user;Password=docker;Timeout=1;Command Timeout=1";

    public static string RedisConnectionString =>
        Environment.GetEnvironmentVariable("AVERIA_TEST_REDIS") ?? "localhost:6379,connectTimeout=500,syncTimeout=500,abortConnect=false";

    public static bool DockerTestsRequired =>
        string.Equals(Environment.GetEnvironmentVariable("AVERIA_REQUIRE_DOCKER_TESTS"), "true", StringComparison.OrdinalIgnoreCase);

    public static async Task<ApiRouteTestFixture?> CreateAsync(
        IDictionary<string, string?>? settings = null,
        bool robloxClient = false,
        bool handleCookies = true)
    {
        if (!await IsInfrastructureAvailableAsync())
        {
            if (DockerTestsRequired)
            {
                throw new InvalidOperationException("Docker-backed API route tests require migrated Postgres and Redis.");
            }

            return null;
        }

        ApplyServiceEnvironment(settings);
        var factory = new ApiServiceApplicationFactory(settings);
        var client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
            HandleCookies = handleCookies,
        });
        client.Timeout = TimeSpan.FromSeconds(15);
        if (robloxClient)
        {
            client.DefaultRequestHeaders.UserAgent.ParseAdd("Roblox/WinInet");
        }

        await FeatureFlags.RefreshOnceAsync();

        return new ApiRouteTestFixture(factory, client);
    }

    public static async Task<bool> IsInfrastructureAvailableAsync()
    {
        return await InfrastructureAvailable.Value;
    }

    public async Task<ApiRouteTestUser> CreateUserAsync(bool totpEnabled = false)
    {
        var username = "ApiRoute" + Guid.NewGuid().ToString("N")[..12];
        const string password = "password123";

        using var users = Roblox.Services.ServiceProvider.GetOrCreate<UsersService>();
        var created = await users.CreateUser(username, password, Gender.Male);

        if (totpEnabled)
        {
            await users.GetOrSetTotp(created.userId);
            await users.UpdateTotpStatus(created.userId, TotpStatus.Enabled);
        }

        return new ApiRouteTestUser(created.userId, username, password);
    }

    private static void ApplyServiceEnvironment(IDictionary<string, string?>? settings)
    {
        foreach (var entry in CreateConfiguration(settings))
        {
            Environment.SetEnvironmentVariable(entry.Key.Replace(":", "__", StringComparison.Ordinal), entry.Value);
        }
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

    private static Dictionary<string, string?> CreateConfiguration(IDictionary<string, string?>? settings)
    {
        var configuration = new Dictionary<string, string?>
        {
            ["Postgres"] = PostgresConnectionString,
            ["Redis"] = RedisConnectionString,
            ["Authorization"] = "ApiRouteTestAuthorization",
            ["RccAuthorization"] = "ApiRouteTestRccAuthorization",
            ["Jwt:Sessions"] = "api-route-test-session-jwt-secret",
            ["BaseUrl"] = "http://localhost",
            ["CdnBaseUrl"] = "http://localhost",
            ["IsCdnEnabled"] = "false",
            ["OwnerUserId:0"] = "1",
        };

        if (settings != null)
        {
            foreach (var setting in settings)
            {
                configuration[setting.Key] = setting.Value;
            }
        }

        return configuration;
    }

    public async ValueTask DisposeAsync()
    {
        Client.Dispose();
        await _factory.DisposeAsync();
    }

    private sealed class ApiServiceApplicationFactory : WebApplicationFactory<Program>
    {
        private readonly IDictionary<string, string?>? _settings;

        public ApiServiceApplicationFactory(IDictionary<string, string?>? settings)
        {
            _settings = settings;
        }

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment("Testing");
            builder.ConfigureServices(services =>
            {
                services.AddExceptionHandler<ApiRouteTestExceptionHandler>();
            });
            builder.ConfigureAppConfiguration((_, configuration) =>
            {
                configuration.AddInMemoryCollection(CreateConfiguration(_settings));
            });
        }
    }

    private sealed class ApiRouteTestExceptionHandler : IExceptionHandler
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

public sealed record ApiRouteTestUser(long UserId, string Username, string Password);
