using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using Roblox.Metrics;

namespace Roblox.ServiceDefaults;

public static class RobloxTelemetryExtensions
{
    public static IServiceCollection AddRobloxTelemetry(
        this IServiceCollection services,
        IConfiguration configuration,
        string serviceName,
        string environmentName)
    {
        var endpoint = configuration["OpenTelemetry:OtlpEndpoint"]
            ?? configuration["OTEL_EXPORTER_OTLP_ENDPOINT"];

        services.AddOpenTelemetry()
            .ConfigureResource(resource => resource
                .AddService(
                    serviceName,
                    serviceVersion: typeof(RobloxMetrics).Assembly.GetName().Version?.ToString(),
                    serviceInstanceId: Environment.MachineName)
                .AddAttributes(new[]
                {
                    new KeyValuePair<string, object>("deployment.environment.name", environmentName),
                }))
            .WithMetrics(metrics =>
            {
                metrics
                    .AddMeter(RobloxMetrics.MeterName)
                    .AddAspNetCoreInstrumentation()
                    .AddHttpClientInstrumentation()
                    .AddRuntimeInstrumentation()
                    .AddView("roblox.database.operation.duration", HistogramView(1, 5, 10, 25, 50, 100, 250, 500, 1000, 2500, 5000))
                    .AddView("roblox.economy.purchase.duration", HistogramView(5, 10, 25, 50, 100, 250, 500, 1000, 2500, 5000))
                    .AddView("roblox.game.server.operation.duration", HistogramView(10, 50, 100, 250, 500, 1000, 2500, 5000, 10000, 30000))
                    .AddView("roblox.render.duration", HistogramView(10, 50, 100, 250, 500, 1000, 2500, 5000, 10000, 30000));

                if (Uri.TryCreate(endpoint, UriKind.Absolute, out var endpointUri))
                {
                    metrics.AddOtlpExporter(options => options.Endpoint = endpointUri);
                }
            });

        return services;
    }

    private static ExplicitBucketHistogramConfiguration HistogramView(params double[] boundaries) => new()
    {
        Boundaries = boundaries,
    };
}
