using System.Diagnostics.Metrics;

namespace Roblox.Metrics;

/// <summary>
/// Shared OpenTelemetry-compatible instruments emitted by Averia services.
/// </summary>
public static class RobloxMetrics
{
    public const string MeterName = "Roblox.Metrics";
    public const string MeterVersion = "2.0.0";

    internal static readonly Meter Meter = new(MeterName, MeterVersion);

    internal static readonly Counter<long> CacheLookups = Meter.CreateCounter<long>(
        "roblox.cache.lookups", "{lookup}", "Cache lookup attempts.");
    internal static readonly Histogram<double> DatabaseDuration = Meter.CreateHistogram<double>(
        "roblox.database.operation.duration", "ms", "Database operation duration.");
    internal static readonly Counter<long> EconomyRobuxVolume = Meter.CreateCounter<long>(
        "roblox.economy.robux.volume", "{robux}", "Robux processed by economy operations.");
    internal static readonly Histogram<double> PurchaseDuration = Meter.CreateHistogram<double>(
        "roblox.economy.purchase.duration", "ms", "Purchase operation duration.");
    internal static readonly Counter<long> PurchaseFailures = Meter.CreateCounter<long>(
        "roblox.economy.purchase.failures", "{failure}", "Purchase failures by reason.");
    internal static readonly Counter<long> UserEvents = Meter.CreateCounter<long>(
        "roblox.user.events", "{event}", "User and authentication events.");
    internal static readonly Counter<long> FloodChecks = Meter.CreateCounter<long>(
        "roblox.flood_check.hits", "{hit}", "Flood-check limit hits.");
    internal static readonly Counter<long> ApplicationGuardEvents = Meter.CreateCounter<long>(
        "roblox.application_guard.events", "{event}", "Application guard decisions.");
    internal static readonly Counter<long> SecurityEvents = Meter.CreateCounter<long>(
        "roblox.security.events", "{event}", "Security-relevant application events.");
    internal static readonly Counter<long> AssetUploadFailures = Meter.CreateCounter<long>(
        "roblox.asset.upload.failures", "{failure}", "Rejected asset uploads.");
    internal static readonly Counter<long> GameJoinEvents = Meter.CreateCounter<long>(
        "roblox.game.join.events", "{event}", "Game join pipeline events.");
    internal static readonly Counter<long> GameServerEvents = Meter.CreateCounter<long>(
        "roblox.game.server.events", "{event}", "Game server lifecycle events.");
    internal static readonly Histogram<double> GameServerDuration = Meter.CreateHistogram<double>(
        "roblox.game.server.operation.duration", "ms", "Game server operation duration.");
    internal static readonly Counter<long> RenderFailures = Meter.CreateCounter<long>(
        "roblox.render.failures", "{failure}", "Render failures.");
    internal static readonly Histogram<double> RenderDuration = Meter.CreateHistogram<double>(
        "roblox.render.duration", "ms", "Render operation duration.");
    internal static readonly Counter<long> AssetRenderQueueEvents = Meter.CreateCounter<long>(
        "roblox.asset_render_queue.events", "{event}", "Durable asset render queue transitions.");
    internal static long AssetRenderReady;
    internal static long AssetRenderDelayed;
    internal static long AssetRenderActive;
    internal static long AssetRenderOldestMilliseconds;
    internal static readonly ObservableGauge<long> AssetRenderReadyGauge = Meter.CreateObservableGauge(
        "roblox.asset_render_queue.ready", () => Volatile.Read(ref AssetRenderReady), "{job}");
    internal static readonly ObservableGauge<long> AssetRenderDelayedGauge = Meter.CreateObservableGauge(
        "roblox.asset_render_queue.delayed", () => Volatile.Read(ref AssetRenderDelayed), "{job}");
    internal static readonly ObservableGauge<long> AssetRenderActiveGauge = Meter.CreateObservableGauge(
        "roblox.asset_render_queue.active", () => Volatile.Read(ref AssetRenderActive), "{job}");
    internal static readonly ObservableGauge<long> AssetRenderOldestGauge = Meter.CreateObservableGauge(
        "roblox.asset_render_queue.oldest", () => Volatile.Read(ref AssetRenderOldestMilliseconds), "ms");
}
