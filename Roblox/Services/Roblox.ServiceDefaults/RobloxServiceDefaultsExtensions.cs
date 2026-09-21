using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Hosting;
using Microsoft.OpenApi;
using Swashbuckle.AspNetCore.SwaggerGen;
using Roblox.Web.Infrastructure;
using Roblox.Web.Infrastructure.Extensions;
using Roblox.Web.Infrastructure.Middleware;

namespace Roblox.ServiceDefaults;

public static class RobloxServiceDefaultsExtensions
{
    public static WebApplicationBuilder AddRobloxServiceDefaults(
        this WebApplicationBuilder builder,
        string serviceName,
        ServiceExposure exposure,
        Action<SwaggerGenOptions>? configureSwagger = null)
    {
        RobloxServiceInfrastructure.Initialize(builder.Configuration);
        builder.Services.AddRobloxWebInfrastructure(builder.Configuration);
        builder.Services.AddRobloxTelemetry(builder.Configuration, serviceName, builder.Environment.EnvironmentName);

        builder.Services.AddExceptionHandler<RobloxServiceExceptionHandler>();
        builder.Services.AddProblemDetails();
        builder.Services.AddEndpointsApiExplorer();
        builder.Services.AddSwaggerGen(options =>
        {
            options.SwaggerDoc("v1", new OpenApiInfo
            {
                Title = serviceName,
                Version = "v1",
            });
            configureSwagger?.Invoke(options);
        });
        builder.Services
            .AddHealthChecks()
            .AddCheck<RobloxInfrastructureHealthCheck>("dependencies", tags: new[] { "ready" });

        return builder;
    }

    public static WebApplication UseRobloxServiceDefaults(this WebApplication app, ServiceExposure exposure)
    {
        Roblox.Services.ServiceProvider.Initialize(app.Services);
        app.UseRouting();
        app.UseRobloxRequestServicesScope();
        app.UseExceptionHandler();

        if (exposure == ServiceExposure.InternalService)
        {
            app.UseMiddleware<ProxyForwardedAuthMiddleware>();
        }
        else if (exposure == ServiceExposure.PublicService)
        {
            app.UseMiddleware<ApiProxyForwardedAuthMiddleware>();
        }

        app.UseMiddleware<RobloxCsrfMiddleware>();

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

        return app;
    }
}
