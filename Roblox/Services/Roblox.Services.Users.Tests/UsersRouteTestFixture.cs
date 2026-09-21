using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using Roblox.Dto.Users;
using Roblox.Models.Users;
using Roblox.Services;
using Roblox.Web.Infrastructure.Http;
using StackExchange.Redis;

namespace Roblox.Services.Users.Tests;

public sealed class UsersRouteTestFixture : IAsyncDisposable
{
    private static readonly Lazy<Task<bool>> InfrastructureAvailable = new(CheckInfrastructureAvailableAsync);

    private readonly UsersServiceApplicationFactory _factory;

    private UsersRouteTestFixture(UsersServiceApplicationFactory factory, HttpClient client)
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

    public static async Task<UsersRouteTestFixture?> CreateAsync()
    {
        if (!await IsInfrastructureAvailableAsync())
        {
            if (DockerTestsRequired)
            {
                throw new InvalidOperationException("Docker-backed Users route tests require migrated Postgres and Redis.");
            }

            return null;
        }

        ApplyServiceEnvironment();
        var factory = new UsersServiceApplicationFactory();
        var client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
        });
        client.Timeout = TimeSpan.FromSeconds(15);

        return new UsersRouteTestFixture(factory, client);
    }

    public static async Task<bool> IsInfrastructureAvailableAsync()
    {
        return await InfrastructureAvailable.Value;
    }

    public async Task<UsersRouteTestUser> CreateUserAsync()
    {
        var username = "UsersRoute" + Guid.NewGuid().ToString("N")[..12];
        const string password = "password123";

        using var users = ServiceProvider.GetOrCreate<UsersService>();
        var created = await users.CreateUser(username, password, Gender.Male);
        return new UsersRouteTestUser(created.userId, username, password);
    }

    public async Task<HttpResponseMessage> SendAsync(
        HttpMethod method,
        string path,
        bool authenticated = false,
        object? jsonBody = null,
        bool includeProxyAuthorization = true,
        UsersRouteTestUser? authenticatedUser = null)
    {
        var request = new HttpRequestMessage(method, path);
        if (includeProxyAuthorization)
        {
            request.Headers.Add(RobloxWebContextConstants.ProxyAuthorizationHeaderName, "UsersRouteTestAuthorization");
        }

        if (authenticated)
        {
            var userId = authenticatedUser?.UserId ?? 1;
            var username = authenticatedUser?.Username ?? "UsersRouteAuthenticated";
            request.Headers.Add(RobloxWebContextConstants.UserIdHeaderName, userId.ToString());
            request.Headers.Add(RobloxWebContextConstants.UsernameHeaderName, username);
            request.Headers.Add(RobloxWebContextConstants.SessionIdHeaderName, Guid.NewGuid().ToString("N"));
            request.Headers.Add(RobloxWebContextConstants.AccountStatusHeaderName, AccountStatus.Ok.ToString());
        }

        if (jsonBody != null)
        {
            request.Content = JsonContent.Create(jsonBody);
        }

        return await Client.SendAsync(request);
    }

    private static void ApplyServiceEnvironment()
    {
        foreach (var entry in CreateConfiguration())
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

    private static Dictionary<string, string?> CreateConfiguration()
    {
        return new Dictionary<string, string?>
        {
            ["Postgres"] = PostgresConnectionString,
            ["Redis"] = RedisConnectionString,
            ["Authorization"] = "UsersRouteTestAuthorization",
            ["RccAuthorization"] = "UsersRouteTestRccAuthorization",
            ["Jwt:Sessions"] = "users-route-test-session-jwt-secret",
            ["BaseUrl"] = "http://localhost",
            ["CdnBaseUrl"] = "http://localhost",
            ["IsCdnEnabled"] = "false",
            ["OwnerUserId:0"] = "1",
        };
    }

    public async ValueTask DisposeAsync()
    {
        Client.Dispose();
        await _factory.DisposeAsync();
    }

    private sealed class UsersServiceApplicationFactory : WebApplicationFactory<Program>
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment("Testing");
            builder.ConfigureServices(services =>
            {
                services.AddExceptionHandler<UsersRouteTestExceptionHandler>();
            });
            builder.ConfigureAppConfiguration((_, configuration) =>
            {
                configuration.AddInMemoryCollection(CreateConfiguration());
            });
        }
    }

    private sealed class UsersRouteTestExceptionHandler : IExceptionHandler
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

public sealed record UsersRouteTestUser(long UserId, string Username, string Password);
