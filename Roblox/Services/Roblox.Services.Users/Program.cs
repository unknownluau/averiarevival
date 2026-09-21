using Roblox.ServiceDefaults;
using Roblox.Services.Users.Services;
using Roblox.Web.Infrastructure;

var builder = WebApplication.CreateBuilder(args);

builder.AddRobloxServiceDefaults("Roblox.Services.Users", ServiceExposure.InternalService);
builder.Services.AddSingleton<StaffPermissionsResolver>();
builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.PropertyNamingPolicy = null;
    });

var app = builder.Build();

app.UseRobloxServiceDefaults(ServiceExposure.InternalService);
app.MapControllers();

app.Run();

public partial class Program
{
}
