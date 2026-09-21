using System.Text.Json.Serialization;
using Roblox.ServiceDefaults;
using Roblox.Services.Thumbnails.ExceptionHandlers;
using Roblox.Web.Infrastructure;
using Roblox.Web.Infrastructure.Http;

var builder = WebApplication.CreateBuilder(args);

builder.AddRobloxServiceDefaults("Roblox.Services.Thumbnails", ServiceExposure.InternalService);
await RobloxIpHasher.InitializeIpHashSetupAsync();
builder.Services.AddExceptionHandler<ThumbnailsServiceExceptionHandler>();
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
