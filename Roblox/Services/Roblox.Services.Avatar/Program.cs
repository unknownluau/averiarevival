using System.Text.Json.Serialization;
using Roblox.ServiceDefaults;
using Roblox.Services.App.FeatureFlags;
using Roblox.Services.Avatar.ExceptionHandlers;
using Roblox.Services.Avatar.HostedServices;
using Roblox.Web.Infrastructure;

var builder = WebApplication.CreateBuilder(args);

builder.AddRobloxServiceDefaults("Roblox.Services.Avatar", ServiceExposure.InternalService);
await FeatureFlags.RefreshOnceAsync();

builder.Services.AddHostedService<FeatureFlagRefreshHostedService>();
builder.Services.AddExceptionHandler<AvatarServiceExceptionHandler>();
builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());
        options.JsonSerializerOptions.PropertyNamingPolicy = null;
    });

var app = builder.Build();

app.UseRobloxServiceDefaults(ServiceExposure.InternalService);
app.MapControllers();

app.Run();

public partial class Program
{
}
