using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Mvc.Formatters;
using Roblox.ServiceDefaults;
using Roblox.Services.Api.HostedServices;
using Roblox.Services.App.FeatureFlags;
using Roblox.Web.Infrastructure;
using Roblox.Web.Infrastructure.Http;

var builder = WebApplication.CreateBuilder(args);

builder.AddRobloxServiceDefaults("Roblox.Services.Api", ServiceExposure.InternalService);
await FeatureFlags.RefreshOnceAsync();
await RobloxIpHasher.InitializeIpHashSetupAsync();

builder.Services.AddHostedService<FeatureFlagRefreshHostedService>();
builder.Services.AddControllers(options =>
    {
        options.InputFormatters.Add(new XmlSerializerInputFormatter(options));
        options.RespectBrowserAcceptHeader = true;
    })
    .AddJsonOptions(o =>
    {
        o.JsonSerializerOptions.Converters.Add(new System.Text.Json.Serialization.JsonStringEnumConverter());
        o.JsonSerializerOptions.PropertyNamingPolicy = null;
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
