using Roblox.Website.Startup;
using Roblox.Web.Infrastructure.Http;
using Roblox.Website.Middleware;

var domain = AppDomain.CurrentDomain;
domain.SetData("REGEX_DEFAULT_MATCH_TIMEOUT", TimeSpan.FromSeconds(5));

var builder = WebApplication.CreateBuilder(args);

builder.InitializeLegacyConfiguration();
await RobloxIpHasher.InitializeIpHashSetupAsync();

builder.Services.AddHttpClient("FrontendProxy", client =>
{
    client.BaseAddress = new Uri("http://127.0.0.1:3000");
    client.Timeout = TimeSpan.FromSeconds(30);
})
.ConfigurePrimaryHttpMessageHandler(() => new SocketsHttpHandler
{
    AllowAutoRedirect = false,
    UseCookies = false,
    PooledConnectionLifetime = TimeSpan.FromMinutes(10),
    PooledConnectionIdleTimeout = TimeSpan.FromMinutes(2),
    MaxConnectionsPerServer = 500
});

builder.Services.AddMemoryCache();


builder.Services.AddRobloxWebsiteServices(builder.Configuration, builder.Environment);

var app = builder.Build();

await app.RunDevelopmentBootstrapAsync();
app.UseStaticFiles();

app.UseMiddleware<FrontendProxyMiddleware>();

app.UseRobloxWebsitePipeline();
app.MapRobloxWebsiteEndpoints();

app.Run();
