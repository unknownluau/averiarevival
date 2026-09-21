using Averia.RccServiceArbiter.Configuration;
using Averia.RccServiceArbiter.Middleware;
using Averia.RccServiceArbiter.Processes;
using Averia.RccServiceArbiter.Rcc;
using Averia.RccServiceArbiter.Rendering;
using Averia.RccServiceArbiter.Services;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Roblox.ServiceDefaults;
using Roblox.Web.Infrastructure;
using Roblox.Web.Infrastructure.Middleware;

var builder = WebApplication.CreateBuilder(args);

builder.AddRobloxServiceDefaults("Averia.RccServiceArbiter", ServiceExposure.InternalService);
builder.Services.Configure<HealthCheckServiceOptions>(options =>
{
    var dependencies = options.Registrations.FirstOrDefault(registration => registration.Name == "dependencies");
    if (dependencies != null)
    {
        options.Registrations.Remove(dependencies);
    }
});
builder.Services.AddHealthChecks()
    .AddCheck<RenderReadinessHealthCheck>("arbiter-render", tags: new[] { "ready" });
builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.PropertyNamingPolicy = null;
    });

builder.Services.AddOptions<ArbiterOptions>()
    .Bind(builder.Configuration.GetSection("Arbiter"))
    .ValidateDataAnnotations()
    .Validate(options => options.Render.MinimumWarmWorkers <= options.Render.MaxWorkers,
        "MinimumWarmWorkers cannot exceed MaxWorkers")
    .Validate(options => options.Render.MaximumIdleWorkers <= options.Render.MaxWorkers,
        "MaximumIdleWorkers cannot exceed MaxWorkers")
    .ValidateOnStart();
builder.Services.AddSingleton<IArbiterClock, SystemArbiterClock>();
builder.Services.AddSingleton<IPortAllocator, PortAllocator>();
builder.Services.AddSingleton<IRccProcessLauncher, RccProcessLauncher>();
builder.Services.AddSingleton<IRccReadinessProbe, TcpRccReadinessProbe>();
builder.Services.AddHttpClient<IRccSoapClientFactory, RccSoapClientFactory>();
builder.Services.AddSingleton<IRccJsonPayloadFactory, RccJsonPayloadFactory>();
builder.Services.AddSingleton<IArbiterPostStartQueue, ArbiterPostStartQueue>();
builder.Services.AddSingleton<IRccProcessPool, RccProcessPool>();
builder.Services.AddSingleton<IRenderScriptCatalog, RenderScriptCatalog>();
builder.Services.AddSingleton<IRenderService, RenderService>();
builder.Services.AddHostedService<RccProcessCleanupService>();
builder.Services.AddHostedService<RenderWorkerCleanupService>();
builder.Services.AddHostedService<ArbiterPostStartWorker>();

var app = builder.Build();

Roblox.Services.ServiceProvider.Initialize(app.Services);
app.UseRouting();
app.UseRobloxRequestServicesScope();
app.UseExceptionHandler();
app.UseMiddleware<ArbiterInternalAuthMiddleware>();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.MapHealthChecks("/health/live", new HealthCheckOptions
{
    Predicate = _ => false,
});
app.MapHealthChecks("/health/ready", new HealthCheckOptions
{
    Predicate = registration => registration.Tags.Contains("ready"),
});
app.MapControllers();

app.Run();

public partial class Program
{
}
