using Microsoft.AspNetCore.Http.Features;
using Roblox.ServiceDefaults;
using Roblox.Services.App.FeatureFlags;
using Roblox.Services.Games;
using Roblox.Services.Games.ExceptionHandlers;
using Roblox.Services.Games.HostedServices;
using Roblox.Web.Infrastructure;
using Roblox.Web.Infrastructure.Http;

var builder = WebApplication.CreateBuilder(args);

builder.AddRobloxServiceDefaults("Roblox.Services.Games", ServiceExposure.InternalService);
await FeatureFlags.RefreshOnceAsync();
await RobloxIpHasher.InitializeIpHashSetupAsync();

builder.Services.AddHostedService<FeatureFlagRefreshHostedService>();
builder.Services.AddExceptionHandler<GamesServiceExceptionHandler>();
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
