using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using Roblox.Models.Users;
using Roblox.Services;
using Roblox.Services.DependencyInjection;
using Roblox.Web.Infrastructure.Auth;
using StackExchange.Redis;

namespace Roblox.Web.Infrastructure.Tests;

public sealed class DockerInfrastructureFixture
{
    private static readonly Lazy<Task<bool>> InfrastructureAvailable = new(CheckInfrastructureAvailableAsync);
    private static readonly object ServiceProviderLock = new();
    private static IServiceProvider? _serviceProvider;

    public static string PostgresConnectionString =>
        Environment.GetEnvironmentVariable("AVERIA_TEST_POSTGRES") ??
        "Host=localhost;Port=5432;Database=roblox_integration_test;Username=roblox_integration_test_user;Password=docker;Timeout=1;Command Timeout=1";

    public static string RedisConnectionString =>
        Environment.GetEnvironmentVariable("AVERIA_TEST_REDIS") ??
        "localhost:6379,connectTimeout=500,syncTimeout=500,abortConnect=false";

    public static bool DockerTestsRequired =>
        string.Equals(Environment.GetEnvironmentVariable("AVERIA_REQUIRE_DOCKER_TESTS"), "true", StringComparison.OrdinalIgnoreCase);

    public static async Task<DockerInfrastructureFixture?> CreateAsync()
    {
        if (!await IsInfrastructureAvailableAsync())
        {
            if (DockerTestsRequired)
            {
                throw new InvalidOperationException("Docker-backed Web.Infrastructure tests require migrated Postgres and Redis.");
            }

            return null;
        }

        ConfigureServiceLayer();
        return new DockerInfrastructureFixture();
    }

    public static Task<bool> IsInfrastructureAvailableAsync()
    {
        return InfrastructureAvailable.Value;
    }

    public async Task<SeededSession> CreateSeededSessionAsync()
    {
        using var users = Roblox.Services.ServiceProvider.GetOrCreate<UsersService>();
        var username = "WebInfra" + Guid.NewGuid().ToString("N")[..12];
        var created = await users.CreateUser(username, "password123", Gender.Male);
        var sessionId = await users.CreateSession(created.userId);
        var cookie = RobloxSessionTokenCodec.CreateJwt(new SessionTokenPayload
        {
            sessionId = sessionId,
            createdAt = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
        });

        return new SeededSession(created.userId, username, sessionId, cookie);
    }

    public async Task DeleteSessionAsync(string sessionId)
    {
        using var users = Roblox.Services.ServiceProvider.GetOrCreate<UsersService>();
        await users.DeleteSession(sessionId);
    }

    public async Task ExpireSessionsAsync(long userId)
    {
        using var users = Roblox.Services.ServiceProvider.GetOrCreate<UsersService>();
        await users.ExpireAllSessions(userId);
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

    private static void ConfigureServiceLayer()
    {
        lock (ServiceProviderLock)
        {
            if (_serviceProvider == null)
            {
                Roblox.Services.Database.Configure(PostgresConnectionString);
                Roblox.Services.Cache.Configure(RedisConnectionString);
                InfrastructureTestHelpers.TryConfigureSessionJwt();

                var services = new ServiceCollection();
                services.AddRobloxServiceLayer();
                _serviceProvider = services.BuildServiceProvider();
            }

            // Other infrastructure tests intentionally replace the legacy static provider
            // with short-lived scopes. Reassert this durable Docker provider for every DB test.
            Roblox.Services.ServiceProvider.Initialize(_serviceProvider);
        }
    }
}

public sealed record SeededSession(long UserId, string Username, string SessionId, string Cookie);
