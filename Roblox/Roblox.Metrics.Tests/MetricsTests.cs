using System.Diagnostics.Metrics;
using System.Reflection;
using Roblox.Metrics;

namespace Roblox.Metrics.Tests;

public sealed class MetricsTests
{
    [Fact]
    public void SignupFromApplication_UsesCorrectBoundedSource()
    {
        using var capture = new MetricCapture<long>("roblox.user.events");

        UserMetrics.ReportSignup(SignupSource.Application);

        var measurement = Assert.Single(capture.Measurements);
        Assert.Equal(1, measurement.Value);
        Assert.Equal("signup", measurement.Tags["user.event"]);
        Assert.Equal("application", measurement.Tags["signup.source"]);
    }

    [Fact]
    public void PurchaseFailure_RecordsOnlyReasonAndProductType()
    {
        using var capture = new MetricCapture<long>("roblox.economy.purchase.failures");

        EconomyMetrics.ReportPurchaseFailure(PurchaseFailureReason.InsufficientFunds, PurchaseProductType.DeveloperProduct);

        var measurement = Assert.Single(capture.Measurements);
        Assert.Equal(new[] { "failure.reason", "purchase.product_type" }, measurement.Tags.Keys.Order().ToArray());
        Assert.Equal("insufficient_funds", measurement.Tags["failure.reason"]);
        Assert.Equal("developer_product", measurement.Tags["purchase.product_type"]);
    }

    [Fact]
    public void DatabaseDuration_UsesMillisecondsAndBoundedOperation()
    {
        using var capture = new MetricCapture<double>("roblox.database.operation.duration");

        PerformanceMetrics.ReportDbDuration(new string('x', 200), 42, true);

        var measurement = Assert.Single(capture.Measurements);
        Assert.Equal(42, measurement.Value);
        Assert.Equal("other", measurement.Tags["db.operation"]);
        Assert.Equal(true, measurement.Tags["db.slow"]);
        Assert.Equal("ms", capture.Unit);
    }

    [Fact]
    public void ReportingApi_IsSynchronous()
    {
        var facadeTypes = typeof(RobloxMetrics).Assembly.GetTypes()
            .Where(type => type.IsAbstract && type.IsSealed && type.Name.EndsWith("Metrics", StringComparison.Ordinal));

        Assert.DoesNotContain(facadeTypes.SelectMany(type => type.GetMethods(BindingFlags.Public | BindingFlags.Static)),
            method => typeof(Task).IsAssignableFrom(method.ReturnType));
    }

    private sealed class MetricCapture<T> : IDisposable where T : struct
    {
        private readonly MeterListener _listener = new();
        public List<CapturedMeasurement<T>> Measurements { get; } = new();
        public string? Unit { get; private set; }

        public MetricCapture(string instrumentName)
        {
            _listener.InstrumentPublished = (instrument, listener) =>
            {
                if (instrument.Meter.Name == RobloxMetrics.MeterName && instrument.Name == instrumentName)
                {
                    Unit = instrument.Unit;
                    listener.EnableMeasurementEvents(instrument);
                }
            };
            _listener.SetMeasurementEventCallback<T>((_, value, tags, _) =>
            {
                var copiedTags = tags.ToArray().ToDictionary(pair => pair.Key, pair => pair.Value);
                Measurements.Add(new CapturedMeasurement<T>(value, copiedTags));
            });
            _listener.Start();
        }

        public void Dispose() => _listener.Dispose();
    }

    private sealed record CapturedMeasurement<T>(T Value, IReadOnlyDictionary<string, object?> Tags) where T : struct;
}
