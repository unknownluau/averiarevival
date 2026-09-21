using System.Globalization;
using System.Net;
using System.Text.Json;
using System.Threading.Channels;
using Dapper;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Roblox.Cache;
using Roblox.Metrics;
using Roblox.Models.Assets;
using StackExchange.Redis;
using AssetType = Roblox.Models.Assets.Type;

namespace Roblox.Services.Assets;

public sealed class AssetRenderQueueOptions
{
    public bool Enabled { get; set; } = true;
    public int FastLaneConcurrency { get; set; } = 2;
    public int FastLaneCapacity { get; set; } = 64;
    public int StreamConcurrency { get; set; } = 2;
    public int ProcessingLeaseSeconds { get; set; } = 120;
    public int JobDeadlineSeconds { get; set; } = 90;
    public int MaximumAttempts { get; set; } = 4;
    public int[] RetryDelaySeconds { get; set; } = [5, 30, 120, 300];
    public int ReconciliationIntervalSeconds { get; set; } = 300;
    public int ReconciliationBatchSize { get; set; } = 100;
}

public static class AssetRenderQueue
{
    public static bool Enabled => Options.Enabled;
    internal const string Stream = "render:assets:v3";
    internal const string Delayed = "render:assets:v3:delayed";
    internal const string DeadLetterStream = "render:assets:v3:dead";
    internal const string Group = "asset-renderers-v3";
    internal const string WakeChannel = "render:assets:v3:wake";
    internal const string DedupPrefix = "render:assets:v3:pending:";
    internal const string LeasePrefix = "render:assets:v3:lease:";
    internal const string FailurePrefix = "render:assets:v3:failed:";

    private static AssetRenderQueueOptions Options { get; set; } = new();
    private static Channel<AssetRenderJob> _fastLane = CreateFastLane(64);

    private const string EnqueueScript = """
        if ARGV[7] == '0' and redis.call('EXISTS', KEYS[3]) == 1 then return false end
        if redis.call('SET', KEYS[1], '1', 'EX', 604800, 'NX') then
          redis.call('DEL', KEYS[3])
          return redis.call('XADD', KEYS[2], '*', 'assetId', ARGV[1], 'versionId', ARGV[2], 'assetType', ARGV[3], 'renderKind', ARGV[4], 'enqueuedAt', ARGV[5], 'attempt', ARGV[6])
        end
        return false
        """;

    public static void Configure(bool enabled)
    {
        Options = new AssetRenderQueueOptions { Enabled = enabled };
        _fastLane = CreateFastLane(Options.FastLaneCapacity);
    }

    public static void Configure(IConfiguration configuration)
    {
        var section = configuration.GetSection("Render:AssetQueue");
        var options = new AssetRenderQueueOptions
        {
            Enabled = configuration.GetValue("Render:UseDurableAssetQueue", true),
            FastLaneConcurrency = Math.Clamp(section.GetValue("FastLaneConcurrency", 2), 1, 32),
            FastLaneCapacity = Math.Clamp(section.GetValue("FastLaneCapacity", 64), 1, 10000),
            StreamConcurrency = Math.Clamp(section.GetValue("StreamConcurrency", 2), 1, 32),
            ProcessingLeaseSeconds = Math.Clamp(section.GetValue("ProcessingLeaseSeconds", 120), 30, 900),
            JobDeadlineSeconds = Math.Clamp(section.GetValue("JobDeadlineSeconds", 90), 30, 600),
            MaximumAttempts = Math.Clamp(section.GetValue("MaximumAttempts", 4), 1, 20),
            ReconciliationIntervalSeconds = Math.Clamp(section.GetValue("ReconciliationIntervalSeconds", 300), 10, 86400),
            ReconciliationBatchSize = Math.Clamp(section.GetValue("ReconciliationBatchSize", 100), 1, 1000),
        };
        var retryDelays = section.GetSection("RetryDelaySeconds").Get<int[]>();
        if (retryDelays is { Length: > 0 })
            options.RetryDelaySeconds = retryDelays.Select(value => Math.Clamp(value, 1, 86400)).ToArray();
        Options = options;
        _fastLane = CreateFastLane(options.FastLaneCapacity);
    }

    internal static AssetRenderQueueOptions GetOptions() => Options;
    internal static ChannelReader<AssetRenderJob> FastLane => _fastLane.Reader;
    internal static bool TryFastLane(AssetRenderJob job) => _fastLane.Writer.TryWrite(job);

    public static bool IsRenderable(AssetType type) => type is
        AssetType.GamePass or AssetType.Badge or AssetType.Image or AssetType.Decal or AssetType.Face or
        AssetType.Shirt or AssetType.Pants or AssetType.Head or AssetType.Torso or AssetType.LeftArm or
        AssetType.RightArm or AssetType.LeftLeg or AssetType.RightLeg or AssetType.Package or AssetType.TShirt or
        AssetType.Animation or AssetType.EmoteAnimation or AssetType.ClimbAnimation or AssetType.DeathAnimation or
        AssetType.FallAnimation or AssetType.IdleAnimation or AssetType.WalkAnimation or AssetType.RunAnimation or
        AssetType.JumpAnimation or AssetType.PoseAnimation or AssetType.SwimAnimation or AssetType.SolidModel or
        AssetType.Model or AssetType.Mesh or AssetType.Hat or AssetType.Gear or AssetType.HairAccessory or
        AssetType.NeckAccessory or AssetType.ShoulderAccessory or AssetType.BackAccessory or AssetType.FrontAccessory or
        AssetType.FaceAccessory or AssetType.WaistAccessory;

    public static async Task<bool> EnqueueAsync(long assetId, long assetVersionId, AssetType assetType,
        string renderKind = "thumbnail", int attempt = 0, bool retryTerminal = true)
    {
        if (!Options.Enabled || !IsRenderable(assetType)) return false;
        var database = DistributedCache.redis.GetDatabase();
        var job = new AssetRenderJob(default, assetId, assetVersionId, assetType, renderKind, attempt,
            DateTimeOffset.UtcNow.ToUnixTimeMilliseconds());
        var result = await database.ScriptEvaluateAsync(EnqueueScript,
            [DedupKey(job), Stream, FailureKey(assetId, assetVersionId)],
            [assetId, assetVersionId, (int)assetType, renderKind, job.EnqueuedAt, attempt, retryTerminal ? 1 : 0]);
        if (result.IsNull) return false;
        job = job with { EntryId = (RedisValue)result };
        _fastLane.Writer.TryWrite(job);
        await database.PublishAsync(RedisChannel.Literal(WakeChannel), job.EntryId);
        RenderMetrics.ReportAssetQueueEvent("enqueued");
        return true;
    }

    public static void Enqueue(long assetId, AssetType assetType) =>
        EnqueueAsync(assetId, 0, assetType).GetAwaiter().GetResult();

    internal static string Identity(AssetRenderJob job) =>
        $"{job.AssetId}:{job.AssetVersionId}:{(int)job.AssetType}:{job.RenderKind}";
    internal static string DedupKey(AssetRenderJob job) => DedupPrefix + Identity(job);
    internal static string LeaseKey(AssetRenderJob job) => LeasePrefix + Identity(job);
    public static string FailureKey(long assetId, long assetVersionId) => FailurePrefix + assetId + ":" + assetVersionId;

    private static Channel<AssetRenderJob> CreateFastLane(int capacity) => Channel.CreateBounded<AssetRenderJob>(
        new BoundedChannelOptions(capacity) { FullMode = BoundedChannelFullMode.DropWrite, SingleWriter = false, SingleReader = false });
}

public sealed class AssetRenderQueueWorker(ILogger<AssetRenderQueueWorker> logger) : BackgroundService
{
    private const string CompleteScript = """
        if redis.call('GET', KEYS[3]) ~= ARGV[3] then return 0 end
        redis.call('XACK', KEYS[1], ARGV[1], ARGV[2])
        redis.call('XDEL', KEYS[1], ARGV[2])
        redis.call('DEL', KEYS[2])
        redis.call('DEL', KEYS[3])
        return 1
        """;
    private const string DelayScript = """
        if redis.call('GET', KEYS[3]) ~= ARGV[5] then return 0 end
        redis.call('ZADD', KEYS[1], ARGV[1], ARGV[2])
        redis.call('XACK', KEYS[2], ARGV[3], ARGV[4])
        redis.call('XDEL', KEYS[2], ARGV[4])
        redis.call('DEL', KEYS[3])
        return 1
        """;
    private const string PromoteScript = """
        if redis.call('ZREM', KEYS[1], ARGV[1]) == 1 then
          return redis.call('XADD', KEYS[2], '*', 'assetId', ARGV[2], 'versionId', ARGV[3], 'assetType', ARGV[4], 'renderKind', ARGV[5], 'enqueuedAt', ARGV[6], 'attempt', ARGV[7])
        end
        return false
        """;
    private const string DeadLetterScript = """
        if redis.call('GET', KEYS[4]) ~= ARGV[3] then return 0 end
        redis.call('XADD', KEYS[1], 'MAXLEN', '~', 10000, '*',
          'assetId', ARGV[4], 'versionId', ARGV[5], 'assetType', ARGV[6],
          'renderKind', ARGV[7], 'failedAt', ARGV[8], 'attempt', ARGV[9],
          'category', ARGV[10], 'message', ARGV[11])
        redis.call('EXPIRE', KEYS[1], 604800)
        redis.call('XACK', KEYS[2], ARGV[1], ARGV[2])
        redis.call('XDEL', KEYS[2], ARGV[2])
        redis.call('DEL', KEYS[3])
        redis.call('DEL', KEYS[4])
        redis.call('SET', KEYS[5], ARGV[10], 'EX', 604800)
        return 1
        """;
    private const string RenewLeaseScript = """
        if redis.call('GET', KEYS[1]) == ARGV[1] then return redis.call('PEXPIRE', KEYS[1], ARGV[2]) end
        return 0
        """;

    private readonly string _consumerPrefix = $"{Environment.MachineName}-{Environment.ProcessId}-{Guid.NewGuid():N}";
    private AssetRenderQueueOptions _options = null!;
    private int _active;
    private long _reconciliationCursor;
    private readonly SemaphoreSlim _streamSignal = new(0, 10000);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!AssetRenderQueue.Enabled) return;
        _options = AssetRenderQueue.GetOptions();
        var database = DistributedCache.redis.GetDatabase();
        await EnsureGroupAsync(database, stoppingToken);
        await DistributedCache.redis.GetSubscriber().SubscribeAsync(RedisChannel.Literal(AssetRenderQueue.WakeChannel),
            (_, _) =>
            {
                try { _streamSignal.Release(); }
                catch (SemaphoreFullException) { }
            });
        for (var index = 0; index < _options.StreamConcurrency; index++) _streamSignal.Release();
        RenderMetrics.ReportAssetQueueEvent("ready");

        var tasks = new List<Task>();
        tasks.AddRange(Enumerable.Range(0, _options.FastLaneConcurrency)
            .Select(index => ConsumeFastLaneAsync(database, $"{_consumerPrefix}-fast-{index}", stoppingToken)));
        tasks.AddRange(Enumerable.Range(0, _options.StreamConcurrency)
            .Select(index => ConsumeStreamAsync(database, $"{_consumerPrefix}-stream-{index}", stoppingToken)));
        tasks.Add(PromoteDelayedAsync(database, stoppingToken));
        tasks.Add(ReconcileAsync(stoppingToken));
        tasks.Add(ReportSnapshotAsync(database, stoppingToken));
        await Task.WhenAll(tasks);
    }

    private async Task ConsumeFastLaneAsync(IDatabase database, string consumer, CancellationToken cancellationToken)
    {
        await foreach (var job in AssetRenderQueue.FastLane.ReadAllAsync(cancellationToken))
            await ProcessIfOwnedAsync(database, job, consumer, cancellationToken);
    }

    private async Task ConsumeStreamAsync(IDatabase database, string consumer, CancellationToken cancellationToken)
    {
        var claimCursor = (RedisValue)"0-0";
        var minimumIdle = Math.Max(_options.ProcessingLeaseSeconds, _options.JobDeadlineSeconds) * 1000L + 30_000;
        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                var claimed = await database.StreamAutoClaimAsync(AssetRenderQueue.Stream, AssetRenderQueue.Group,
                    consumer, minimumIdle, claimCursor, 1);
                claimCursor = claimed.NextStartId;
                if (claimed.ClaimedEntries.Length > 0)
                {
                    RenderMetrics.ReportAssetQueueEvent("reclaimed");
                    await ProcessEntryAsync(database, claimed.ClaimedEntries[0], consumer, cancellationToken);
                    continue;
                }

                var entries = await database.StreamReadGroupAsync(AssetRenderQueue.Stream, AssetRenderQueue.Group,
                    consumer, ">", 1);
                if (entries.Length > 0)
                {
                    await ProcessEntryAsync(database, entries[0], consumer, cancellationToken);
                    continue;
                }

                // StackExchange.Redis multiplexes a connection, so issuing Redis BLOCK
                // commands would stall unrelated cache traffic. Pub/sub wakes consumers
                // immediately across hosts; the timeout only recovers missed notifications.
                await _streamSignal.WaitAsync(TimeSpan.FromSeconds(5), cancellationToken);
            }
            catch (Exception ex) when (ex is RedisConnectionException or RedisTimeoutException)
            {
                logger.LogWarning(ex, "Asset render queue is temporarily unavailable");
                await Task.Delay(TimeSpan.FromSeconds(2), cancellationToken);
            }
            catch (RedisServerException ex) when (ex.Message.Contains("NOGROUP", StringComparison.OrdinalIgnoreCase))
            {
                await EnsureGroupAsync(database, cancellationToken);
            }
        }
    }

    private async Task ProcessEntryAsync(IDatabase database, StreamEntry entry, string consumer,
        CancellationToken cancellationToken)
    {
        try { await ProcessIfOwnedAsync(database, AssetRenderJob.Parse(entry), consumer, cancellationToken); }
        catch (Exception ex) when (ex is FormatException or KeyNotFoundException or OverflowException)
        {
            logger.LogError(ex, "Discarding malformed asset render queue entry {EntryId}", entry.Id);
            await database.StreamAcknowledgeAsync(AssetRenderQueue.Stream, AssetRenderQueue.Group, entry.Id);
            await database.StreamDeleteAsync(AssetRenderQueue.Stream, [entry.Id]);
            RenderMetrics.ReportAssetQueueEvent("terminal", "malformed");
        }
    }

    private async Task ProcessIfOwnedAsync(IDatabase database, AssetRenderJob job, string consumer,
        CancellationToken stoppingToken)
    {
        var leaseToken = Guid.NewGuid().ToString("N");
        var leaseKey = AssetRenderQueue.LeaseKey(job);
        if (!await database.StringSetAsync(leaseKey, leaseToken,
                TimeSpan.FromSeconds(_options.ProcessingLeaseSeconds), When.NotExists)) return;

        Interlocked.Increment(ref _active);
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(_options.JobDeadlineSeconds));
        using var ownershipLost = new CancellationTokenSource();
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(stoppingToken, deadline.Token, ownershipLost.Token);
        using var renewalCancellation = CancellationTokenSource.CreateLinkedTokenSource(stoppingToken);
        var renewal = RenewLeaseAsync(database, leaseKey, leaseToken, ownershipLost, renewalCancellation.Token);
        try
        {
            using var assets = ServiceProvider.GetOrCreate<AssetsService>();
            var latest = await assets.GetLatestAssetVersion(job.AssetId);
            if (job.AssetVersionId == 0 || latest.assetVersionId != job.AssetVersionId)
            {
                await AssetRenderQueue.EnqueueAsync(job.AssetId, latest.assetVersionId, job.AssetType, job.RenderKind);
            }
            else
            {
                await assets.RenderAssetAsync(job.AssetId, job.AssetType, linked.Token, job.AssetVersionId);
                var current = await assets.GetLatestAssetVersion(job.AssetId);
                if (current.assetVersionId != job.AssetVersionId)
                    await AssetRenderQueue.EnqueueAsync(job.AssetId, current.assetVersionId, job.AssetType, job.RenderKind);
            }

            if (!await CompleteAsync(database, job, leaseToken)) throw new AssetRenderLeaseLostException();
            RenderMetrics.ReportAssetQueueEvent("completed");
            logger.LogInformation("Asset render completed for asset {AssetId}, version {VersionId}, attempt {Attempt}, consumer {Consumer}",
                job.AssetId, job.AssetVersionId, job.Attempt, consumer);
        }
        catch (StaleAssetRenderException ex)
        {
            await AssetRenderQueue.EnqueueAsync(job.AssetId, ex.CurrentVersionId, job.AssetType, job.RenderKind);
            if (!await CompleteAsync(database, job, leaseToken))
                logger.LogWarning("Could not complete stale asset render {AssetId}:{VersionId} because its lease changed",
                    job.AssetId, job.AssetVersionId);
            RenderMetrics.ReportAssetQueueEvent("stale");
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { throw; }
        catch (Exception ex) when (ex is AssetRenderLeaseLostException || ownershipLost.IsCancellationRequested)
        {
            RenderMetrics.ReportAssetQueueEvent("lease_lost");
            logger.LogWarning("Asset render ownership was lost for asset {AssetId}, version {VersionId}, consumer {Consumer}",
                job.AssetId, job.AssetVersionId, consumer);
        }
        catch (Exception ex)
        {
            var category = Classify(ex);
            logger.LogError(ex,
                "Asset render failed for asset {AssetId}, version {VersionId}, attempt {Attempt}, consumer {Consumer}, category {Category}",
                job.AssetId, job.AssetVersionId, job.Attempt, consumer, category);
            try
            {
                if (category == AssetRenderFailure.Transient && job.Attempt + 1 < _options.MaximumAttempts)
                {
                    await DelayRetryAsync(database, job with { Attempt = job.Attempt + 1 }, leaseToken);
                    RenderMetrics.ReportAssetQueueEvent("retried", category.ToString().ToLowerInvariant());
                }
                else
                {
                    await DeadLetterAsync(database, job, leaseToken, category, ex.Message);
                    RenderMetrics.ReportAssetQueueEvent("terminal", category.ToString().ToLowerInvariant());
                }
            }
            catch (AssetRenderLeaseLostException)
            {
                RenderMetrics.ReportAssetQueueEvent("lease_lost");
            }
        }
        finally
        {
            renewalCancellation.Cancel();
            try { await renewal; }
            catch (OperationCanceledException) { }
            catch (Exception ex) { logger.LogWarning(ex, "Asset render lease renewal stopped unexpectedly"); }
            Interlocked.Decrement(ref _active);
        }
    }

    private async Task RenewLeaseAsync(IDatabase database, string leaseKey, string token,
        CancellationTokenSource ownershipLost, CancellationToken cancellationToken)
    {
        var delay = TimeSpan.FromSeconds(Math.Max(10, _options.ProcessingLeaseSeconds / 3));
        while (!cancellationToken.IsCancellationRequested)
        {
            await Task.Delay(delay, cancellationToken);
            long renewed;
            try
            {
                renewed = (long)await database.ScriptEvaluateAsync(RenewLeaseScript, [leaseKey],
                    [token, _options.ProcessingLeaseSeconds * 1000]);
            }
            catch
            {
                ownershipLost.Cancel();
                throw;
            }
            if (renewed == 0)
            {
                ownershipLost.Cancel();
                return;
            }
        }
    }

    private async Task DelayRetryAsync(IDatabase database, AssetRenderJob job, string leaseToken)
    {
        var delays = _options.RetryDelaySeconds;
        var delaySeconds = delays[Math.Min(job.Attempt - 1, delays.Length - 1)];
        var due = DateTimeOffset.UtcNow.AddSeconds(delaySeconds).ToUnixTimeMilliseconds();
        var payload = JsonSerializer.Serialize(new DelayedAssetRenderJob(job.AssetId, job.AssetVersionId,
            job.AssetType, job.RenderKind, job.Attempt, job.EnqueuedAt));
        var result = (long)await database.ScriptEvaluateAsync(DelayScript,
            [AssetRenderQueue.Delayed, AssetRenderQueue.Stream, AssetRenderQueue.LeaseKey(job)],
            [due, payload, AssetRenderQueue.Group, job.EntryId, leaseToken]);
        if (result == 0) throw new AssetRenderLeaseLostException();
    }

    private async Task PromoteDelayedAsync(IDatabase database, CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                var due = await database.SortedSetRangeByScoreAsync(AssetRenderQueue.Delayed, stop: DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(), take: 100);
                foreach (var value in due)
                {
                    var delayed = JsonSerializer.Deserialize<DelayedAssetRenderJob>((string)value!)!;
                    var job = new AssetRenderJob(default, delayed.AssetId, delayed.AssetVersionId,
                        delayed.AssetType, delayed.RenderKind, delayed.Attempt, delayed.EnqueuedAt);
                    var result = await database.ScriptEvaluateAsync(PromoteScript,
                        [AssetRenderQueue.Delayed, AssetRenderQueue.Stream],
                        [value, job.AssetId, job.AssetVersionId, (int)job.AssetType, job.RenderKind,
                            DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(), job.Attempt]);
                    if (!result.IsNull)
                    {
                        job = job with { EntryId = (RedisValue)result, EnqueuedAt = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() };
                        AssetRenderQueue.TryFastLane(job);
                        await database.PublishAsync(RedisChannel.Literal(AssetRenderQueue.WakeChannel), job.EntryId);
                        RenderMetrics.ReportAssetQueueEvent("promoted");
                    }
                }
            }
            catch (Exception ex) when (!cancellationToken.IsCancellationRequested)
            {
                logger.LogWarning(ex, "Could not promote delayed asset render jobs");
            }
            await Task.Delay(TimeSpan.FromSeconds(1), cancellationToken);
        }
    }

    private async Task DeadLetterAsync(IDatabase database, AssetRenderJob job, string leaseToken,
        AssetRenderFailure category, string message)
    {
        var result = (long)await database.ScriptEvaluateAsync(DeadLetterScript,
            [AssetRenderQueue.DeadLetterStream, AssetRenderQueue.Stream, AssetRenderQueue.DedupKey(job),
                AssetRenderQueue.LeaseKey(job), AssetRenderQueue.FailureKey(job.AssetId, job.AssetVersionId)],
            [AssetRenderQueue.Group, job.EntryId, leaseToken, job.AssetId, job.AssetVersionId,
                (int)job.AssetType, job.RenderKind, DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(), job.Attempt,
                category.ToString(), Truncate(message, 512)]);
        if (result == 0) throw new AssetRenderLeaseLostException();
    }

    private static string Truncate(string value, int length) => value.Length <= length ? value : value[..length];

    private static AssetRenderFailure Classify(Exception exception)
    {
        if (exception is HttpRequestException { StatusCode: HttpStatusCode.BadRequest or HttpStatusCode.UnprocessableEntity })
            return AssetRenderFailure.Terminal;
        if (exception is ArgumentException or InvalidDataException or NotSupportedException)
            return AssetRenderFailure.Terminal;
        return AssetRenderFailure.Transient;
    }

    private static async Task<bool> CompleteAsync(IDatabase database, AssetRenderJob job, string leaseToken)
    {
        var result = (long)await database.ScriptEvaluateAsync(CompleteScript,
            [AssetRenderQueue.Stream, AssetRenderQueue.DedupKey(job), AssetRenderQueue.LeaseKey(job)],
            [AssetRenderQueue.Group, job.EntryId, leaseToken]);
        return result != 0;
    }

    private async Task EnsureGroupAsync(IDatabase database, CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                await database.StreamCreateConsumerGroupAsync(AssetRenderQueue.Stream, AssetRenderQueue.Group, "0-0", true);
                return;
            }
            catch (RedisServerException ex) when (ex.Message.Contains("BUSYGROUP", StringComparison.OrdinalIgnoreCase)) { return; }
            catch (Exception ex) when (ex is RedisConnectionException or RedisTimeoutException)
            {
                logger.LogWarning(ex, "Waiting for Redis before starting the asset render queue");
                await Task.Delay(TimeSpan.FromSeconds(2), cancellationToken);
            }
        }
    }

    private async Task ReconcileAsync(CancellationToken cancellationToken)
    {
        var renderableTypes = Enum.GetValues<AssetType>().Where(AssetRenderQueue.IsRenderable).Select(value => (int)value).ToArray();
        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                using var assets = ServiceProvider.GetOrCreate<AssetsService>();
                var missing = await assets.db.QueryAsync<(long assetId, long assetVersionId, AssetType assetType)>("""
                    SELECT asset.id AS assetId, asset_version.id AS assetVersionId, asset.asset_type AS assetType
                    FROM asset
                    JOIN LATERAL (SELECT id FROM asset_version WHERE asset_id = asset.id ORDER BY version_number DESC LIMIT 1) asset_version ON TRUE
                    LEFT JOIN asset_thumbnail ON asset_thumbnail.asset_id = asset.id AND asset_thumbnail.asset_version_id = asset_version.id
                    WHERE asset.moderation_status = ANY(:statuses) AND asset_thumbnail.asset_id IS NULL
                      AND asset.asset_type = ANY(:types)
                      AND asset.id > :cursor
                    ORDER BY asset.id LIMIT :limit
                    """, new
                {
                    statuses = new[] { (int)ModerationStatus.ReviewApproved, (int)ModerationStatus.AwaitingApproval,
                        (int)ModerationStatus.AwaitingModerationDecision },
                    types = renderableTypes,
                    cursor = _reconciliationCursor,
                    limit = _options.ReconciliationBatchSize,
                });
                var batch = missing.ToArray();
                foreach (var item in batch)
                    await AssetRenderQueue.EnqueueAsync(item.assetId, item.assetVersionId, item.assetType,
                        retryTerminal: false);
                _reconciliationCursor = batch.Length == 0 ? 0 : batch[^1].assetId;
            }
            catch (Exception ex) when (!cancellationToken.IsCancellationRequested)
            {
                logger.LogWarning(ex, "Asset thumbnail reconciliation failed");
            }
            await Task.Delay(TimeSpan.FromSeconds(_options.ReconciliationIntervalSeconds), cancellationToken);
        }
    }

    private async Task ReportSnapshotAsync(IDatabase database, CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                var ready = await database.StreamLengthAsync(AssetRenderQueue.Stream);
                var delayed = await database.SortedSetLengthAsync(AssetRenderQueue.Delayed);
                var oldest = await database.StreamRangeAsync(AssetRenderQueue.Stream, count: 1, messageOrder: Order.Ascending);
                var age = oldest.Length == 0 ? 0 : Math.Max(0,
                    DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() - AssetRenderJob.Parse(oldest[0]).EnqueuedAt);
                RenderMetrics.SetAssetQueueSnapshot(ready, delayed, Volatile.Read(ref _active), age);
            }
            catch (Exception ex) when (!cancellationToken.IsCancellationRequested)
            {
                logger.LogDebug(ex, "Could not collect asset render queue metrics");
            }
            await Task.Delay(TimeSpan.FromSeconds(10), cancellationToken);
        }
    }
}

internal enum AssetRenderFailure { Transient, Terminal }

internal sealed class AssetRenderLeaseLostException : Exception { }

internal sealed record DelayedAssetRenderJob(long AssetId, long AssetVersionId, AssetType AssetType,
    string RenderKind, int Attempt, long EnqueuedAt);

internal sealed record AssetRenderJob(RedisValue EntryId, long AssetId, long AssetVersionId, AssetType AssetType,
    string RenderKind, int Attempt, long EnqueuedAt)
{
    public static AssetRenderJob Parse(StreamEntry entry) => Parse(entry.Id, entry.Values);

    public static AssetRenderJob Parse(RedisValue entryId, NameValueEntry[] values)
    {
        var fields = values.ToDictionary(value => value.Name.ToString(), value => value.Value.ToString(), StringComparer.Ordinal);
        return new AssetRenderJob(entryId,
            long.Parse(fields["assetId"], CultureInfo.InvariantCulture),
            long.Parse(fields["versionId"], CultureInfo.InvariantCulture),
            (AssetType)int.Parse(fields["assetType"], CultureInfo.InvariantCulture), fields["renderKind"],
            int.Parse(fields["attempt"], CultureInfo.InvariantCulture),
            long.Parse(fields["enqueuedAt"], CultureInfo.InvariantCulture));
    }
}

public sealed class StaleAssetRenderException(long expectedVersionId, long currentVersionId)
    : Exception($"Asset version changed from {expectedVersionId} to {currentVersionId} while rendering")
{
    public long ExpectedVersionId { get; } = expectedVersionId;
    public long CurrentVersionId { get; } = currentVersionId;
}
