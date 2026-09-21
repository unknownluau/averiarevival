using Microsoft.AspNetCore.Http.Features;
using Roblox.ServiceDefaults;
using Roblox.Services.Admin.HostedServices;
using Roblox.Services.Admin.Telemetry;
using Roblox.Services.App.FeatureFlags;
using Roblox.Web.Infrastructure;
using Roblox.Web.Infrastructure.Admin;

var builder = WebApplication.CreateBuilder(args);
Roblox.Services.Assets.AssetRenderQueue.Configure(builder.Configuration);

builder.AddRobloxServiceDefaults("Roblox.Services.Admin", ServiceExposure.InternalService);
await FeatureFlags.RefreshOnceAsync();
builder.Services.AddSingleton<IAdminStaffAuthorizationService, AdminStaffAuthorizationService>();
builder.Services.AddSingleton<IAdminTwoFactorStore, AdminTwoFactorStore>();
builder.Services.AddHostedService<FeatureFlagRefreshHostedService>();
builder.Services.AddHostedService<Roblox.Services.Assets.AssetRenderQueueWorker>();
builder.Services.AddMemoryCache();
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddHttpClient<IRenderStatisticsClient, ArbiterRenderStatisticsClient>((serviceProvider, client) =>
{
    var configuration = serviceProvider.GetRequiredService<IConfiguration>();
    var baseUrl = configuration["Render:BaseUrl"] ?? "http://127.0.0.1:3521/";
    client.BaseAddress = new Uri(baseUrl.EndsWith('/') ? baseUrl : baseUrl + "/");
    client.Timeout = TimeSpan.FromSeconds(2);
    var authorization = configuration["ArbiterAuthorization"] ?? configuration["Authorization"];
    if (!string.IsNullOrWhiteSpace(authorization))
        client.DefaultRequestHeaders.TryAddWithoutValidation("rblx-authorization", authorization);
});
builder.Services.AddHttpClient<ITelemetryQueryService, PrometheusTelemetryQueryService>((serviceProvider, client) =>
{
    var configuration = serviceProvider.GetRequiredService<IConfiguration>();
    var baseUrl = configuration["Telemetry:PrometheusBaseUrl"] ?? "http://prometheus:9090/";
    client.BaseAddress = new Uri(baseUrl.EndsWith('/') ? baseUrl : baseUrl + "/");
    client.Timeout = TimeSpan.FromSeconds(5);
});
builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.Converters.Add(new System.Text.Json.Serialization.JsonStringEnumConverter());
        options.JsonSerializerOptions.PropertyNamingPolicy = null;
    });
builder.Services.Configure<FormOptions>(options =>
{
    options.ValueLengthLimit = int.MaxValue;
    options.MultipartBodyLengthLimit = long.MaxValue;
});

var app = builder.Build();

app.UseRobloxServiceDefaults(ServiceExposure.InternalService);
app.MapControllers();

app.Run();

public partial class Program
{
}
