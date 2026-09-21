using System.Net;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Roblox.Models.Sessions;
using Roblox.Models.Users;
using Roblox.Services;
using Roblox.Web.Infrastructure.Configuration;
using Roblox.Web.Infrastructure.Http;
using Roblox.Web.Infrastructure.Services;
using Roblox.Website.Controllers;
using Roblox.Website.HostedServices;

namespace Roblox.Web.Infrastructure.Tests;

public class MachineValidationRouteTests
{
    [Fact]
    public async Task MachineBannedSession_ReturnsNormalNotFoundAndSchedulesDurableEnforcement()
    {
        if (await DockerInfrastructureFixture.CreateAsync() == null)
            return;

        using var users = Roblox.Services.ServiceProvider.GetOrCreate<UsersService>();
        using var machineBans = Roblox.Services.ServiceProvider.GetOrCreate<MachineBanService>();
        var source = await users.CreateUser(
            "ValidateMachine" + Guid.NewGuid().ToString("N")[..10],
            "password123",
            Gender.Male);
        await machineBans.ActivateAsync(source.userId, 1, "private machine-ban reason");

        using var host = CreateHost(source.userId);
        using var request = new HttpRequestMessage(HttpMethod.Post, "/game/validate-machine")
        {
            Content = new FormUrlEncodedContent(new[]
            {
                new KeyValuePair<string, string>("macAddresses", "AABBCCDDEE10"),
            }),
        };
        request.Headers.UserAgent.ParseAdd("Roblox/WinInet");
        using var response = await host.CreateClient().SendAsync(request, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Empty(await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
        Assert.Contains(
            await users.GetMacAddresses(source.userId),
            entry => entry?.macAddress == "AABBCCDDEE10");

        await using var connection = new Npgsql.NpgsqlConnection(DockerInfrastructureFixture.PostgresConnectionString);
        await connection.OpenAsync(TestContext.Current.CancellationToken);
        await using var command = new Npgsql.NpgsqlCommand(
            "SELECT COUNT(*) FROM machine_ban_enforcement WHERE user_id = @user_id AND completed_at IS NULL",
            connection);
        command.Parameters.AddWithValue("user_id", source.userId);
        Assert.Equal(1L, (long)(await command.ExecuteScalarAsync(TestContext.Current.CancellationToken))!);
    }

    private static TestServer CreateHost(long userId)
    {
        return new TestServer(new WebHostBuilder()
            .ConfigureServices(services =>
            {
                services.AddRouting();
                services.AddHttpContextAccessor();
                services.AddOptions<RobloxWebInfrastructureOptions>();
                services.AddScoped(_ => new RobloxServiceAccessor());
                services.AddSingleton<IRobloxRequestContextAccessor, RobloxRequestContextAccessor>();
                services.AddSingleton<FileContentCache>();
                services.AddSingleton<MachineBanEnforcementSignal>();
                services.AddControllers().AddApplicationPart(typeof(BypassController).Assembly);
            })
            .Configure(app =>
            {
                app.UseRouting();
                app.Use(async (context, next) =>
                {
                    context.SetRobloxRequestContext(new RobloxRequestContext
                    {
                        IsAuthenticated = true,
                        IsRobloxClient = true,
                        UserAgent = "Roblox/WinInet",
                        Session = new UserSession(
                            userId,
                            "machine-route-test",
                            DateTime.UtcNow,
                            AccountStatus.MachineBanned,
                            1,
                            false,
                            "machine-route-session"),
                    });
                    await next();
                });
                app.UseEndpoints(endpoints => endpoints.MapControllers());
            }));
    }
}
