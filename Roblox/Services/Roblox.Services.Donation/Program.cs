using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Roblox.ServiceDefaults;
using Roblox.Web.Infrastructure;

var builder = WebApplication.CreateBuilder(args);
builder.AddRobloxServiceDefaults("Roblox.Services.Donation", ServiceExposure.InternalService);
builder.Services.AddHttpClient();
builder.Services.AddSingleton<Roblox.Services.Donation.Services.DonationDiscordNotifier>();
builder.Services.AddControllers().AddJsonOptions(options =>
{
    options.JsonSerializerOptions.PropertyNamingPolicy = null;
});

var app = builder.Build();
app.UseRobloxServiceDefaults(ServiceExposure.InternalService);
app.MapControllers();
app.Run();
