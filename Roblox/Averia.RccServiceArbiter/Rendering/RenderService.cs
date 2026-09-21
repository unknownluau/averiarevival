using System.Collections.Concurrent;
using System.Diagnostics;
using Averia.RccServiceArbiter.Configuration;
using Averia.RccServiceArbiter.Processes;
using Averia.RccServiceArbiter.Rcc;
using Microsoft.Extensions.Options;
using Roblox.Rendering;

namespace Averia.RccServiceArbiter.Rendering;

public sealed class RenderService : IRenderService, IDisposable
{
    private readonly object _workersGate = new();
    private readonly List<RenderWorker> _idle = [];
    private readonly PriorityRenderGate _renderGate;
    private readonly SemaphoreSlim _conversionSlots;
    private readonly ArbiterOptions _options;
    private readonly IPortAllocator _ports;
    private readonly IRccProcessLauncher _launcher;
    private readonly IRccReadinessProbe _readiness;
    private readonly IRccSoapClientFactory _soapFactory;
    private readonly IRenderScriptCatalog _scripts;
    private readonly ILogger<RenderService> _logger;
    private readonly ConcurrentDictionary<string, Lazy<Task<RenderOutput>>> _inflight = new(StringComparer.Ordinal);
    private int _workerCount;
    private int _running;
    private int _conversionQueued;
    private long _completed;
    private long _failed;
    private long _coldStarts;
    private long _reusedWorkers;
    private long _coalesced;
    private long _retries;
    private long _outputBytes;
    private long _queueMilliseconds;
    private long _rccMilliseconds;
    private long _totalMilliseconds;
    private volatile bool _ready;

    public RenderService(IOptions<ArbiterOptions> options, IPortAllocator ports, IRccProcessLauncher launcher,
        IRccReadinessProbe readiness, IRccSoapClientFactory soapFactory, IRenderScriptCatalog scripts,
        ILogger<RenderService> logger)
    {
        _options = options.Value;
        _ports = ports;
        _launcher = launcher;
        _readiness = readiness;
        _soapFactory = soapFactory;
        _scripts = scripts;
        _logger = logger;
        _renderGate = new PriorityRenderGate(_options.Render.MaxWorkers,
            _options.Render.InteractiveQueueCapacity, _options.Render.BackgroundQueueCapacity);
        _conversionSlots = new SemaphoreSlim(_options.Render.ConversionConcurrency, _options.Render.ConversionConcurrency);
    }

    public bool IsReady => _ready;

    public async Task<RenderOutput> RenderAsync(RenderRequest request, CancellationToken cancellationToken)
    {
        Validate(request);
        if (string.IsNullOrWhiteSpace(request.WorkKey))
            return await RenderCoreAsync(request, cancellationToken);

        var workKey = $"{request.Kind}:{request.Width}x{request.Height}:{request.AvatarRigType}:{request.WorkKey}";
        if (workKey.Length > 256) throw new RenderValidationException("workKey is too long");
        var candidate = new Lazy<Task<RenderOutput>>(
            () => RenderCoreAsync(request, CancellationToken.None), LazyThreadSafetyMode.ExecutionAndPublication);
        var shared = _inflight.GetOrAdd(workKey, candidate);
        if (!ReferenceEquals(candidate, shared))
        {
            Interlocked.Increment(ref _coalesced);
        }

        var task = shared.Value;
        if (ReferenceEquals(candidate, shared))
        {
            _ = task.ContinueWith(static (_, state) =>
            {
                var cleanup = ((RenderService Service, string Key, Lazy<Task<RenderOutput>> Entry))state!;
                cleanup.Service._inflight.TryRemove(
                    new KeyValuePair<string, Lazy<Task<RenderOutput>>>(cleanup.Key, cleanup.Entry));
            }, (this, workKey, shared), CancellationToken.None,
                TaskContinuationOptions.ExecuteSynchronously, TaskScheduler.Default);
        }

        return await task.WaitAsync(cancellationToken);
    }

    private async Task<RenderOutput> RenderCoreAsync(RenderRequest request, CancellationToken cancellationToken)
    {
        var totalWatch = Stopwatch.StartNew();
        var totalSeconds = Math.Clamp(request.DeadlineSeconds ??
            (_options.Render.JobTimeoutSeconds + _options.Processes.StartupTimeoutSeconds), 1, 300);
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(totalSeconds));
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, deadline.Token);

        try
        {
            RenderOutput output;
            if (request.Kind is RenderKind.PlaceConversion or RenderKind.HatConversion)
                output = await ExecuteConversionAsync(request, linked.Token);
            else
                output = await ExecuteRccAsync(request, linked.Token);

            Interlocked.Increment(ref _completed);
            Interlocked.Add(ref _totalMilliseconds, (long)totalWatch.Elapsed.TotalMilliseconds);
            _logger.LogInformation("Render {RenderKind} completed in {TotalMilliseconds}ms on a {WorkerState} worker",
                request.Kind, totalWatch.Elapsed.TotalMilliseconds, output.WorkerState);
            return output;
        }
        catch (OperationCanceledException) when (deadline.IsCancellationRequested && !cancellationToken.IsCancellationRequested)
        {
            Interlocked.Increment(ref _failed);
            _logger.LogWarning("Render {RenderKind} timed out after {TotalMilliseconds}ms",
                request.Kind, totalWatch.Elapsed.TotalMilliseconds);
            throw new TimeoutException("Render request timed out");
        }
        catch
        {
            Interlocked.Increment(ref _failed);
            throw;
        }
    }

    private async Task<RenderOutput> ExecuteRccAsync(RenderRequest request, CancellationToken cancellationToken)
    {
        var queueWatch = Stopwatch.StartNew();
        using var lease = await _renderGate.WaitAsync(request.Priority, cancellationToken);
        queueWatch.Stop();
        Interlocked.Add(ref _queueMilliseconds, (long)queueWatch.Elapsed.TotalMilliseconds);
        Interlocked.Increment(ref _running);
        try
        {
            Exception? firstFailure = null;
            for (var attempt = 0; attempt < 2; attempt++)
            {
                RenderWorker? worker = null;
                var healthy = false;
                try
                {
                    var acquireWatch = Stopwatch.StartNew();
                    var acquired = await AcquireWorkerAsync(cancellationToken);
                    worker = acquired.Worker;
                    acquireWatch.Stop();
                    var workerState = acquired.Cold ? "cold" : "warm";

                    var jobId = Guid.NewGuid();
                    request.CorrelationId = jobId.ToString("N");
                    var script = _scripts.Create(request);
                    var job = new Job
                    {
                        Id = jobId.ToString(), Category = 2, Cores = 1,
                        ExpirationInSeconds = _options.Render.JobTimeoutSeconds,
                    };
                    var rccWatch = Stopwatch.StartNew();
                    var renderResponse = await worker.Soap.BatchRenderAsync(job, script,
                        _options.Render.DefaultYear >= 2018, request.Kind == RenderKind.Avatar3D,
                        _options.Render.MaxOutputMegabytes * 1024 * 1024, cancellationToken);
                    rccWatch.Stop();
                    Interlocked.Add(ref _rccMilliseconds, (long)rccWatch.Elapsed.TotalMilliseconds);

                    var decodeWatch = Stopwatch.StartNew();
                    var data = renderResponse.Data;
                    if (data.Length == 0) throw new RenderExecutionException("RCC returned no render data");
                    decodeWatch.Stop();
                    if (data.LongLength > _options.Render.MaxOutputMegabytes * 1024L * 1024L)
                        throw new RenderExecutionException("RCC render output exceeded the configured limit");

                    healthy = true;
                    worker.UseCount++;
                    worker.LastUsedUtc = DateTime.UtcNow;
                    Interlocked.Add(ref _outputBytes, data.LongLength);
                    return new RenderOutput
                    {
                        JobId = jobId,
                        ContentType = request.Kind == RenderKind.Avatar3D ? "application/json" : "image/png",
                        Data = data,
                        WorkerState = workerState,
                        DependencyUrls = renderResponse.DependencyUrls,
                        Timings = new Dictionary<string, double>
                        {
                            ["queue"] = queueWatch.Elapsed.TotalMilliseconds,
                            ["acquire"] = acquireWatch.Elapsed.TotalMilliseconds,
                            ["rcc"] = rccWatch.Elapsed.TotalMilliseconds,
                            ["decode"] = decodeWatch.Elapsed.TotalMilliseconds,
                        },
                    };
                }
                catch (Exception ex) when (attempt == 0 && ex is HttpRequestException or IOException)
                {
                    firstFailure = ex;
                    Interlocked.Increment(ref _retries);
                    _logger.LogWarning(ex, "RCC render attempt failed; retrying on a fresh worker");
                }
                finally
                {
                    if (worker != null) ReleaseWorker(worker, healthy);
                }
            }
            throw firstFailure ?? new RenderExecutionException("RCC render failed");
        }
        finally
        {
            Interlocked.Decrement(ref _running);
        }
    }

    private async Task<RenderOutput> ExecuteConversionAsync(RenderRequest request, CancellationToken cancellationToken)
    {
        var queued = Interlocked.Increment(ref _conversionQueued);
        if (queued > _options.Render.ConversionQueueCapacity + _options.Render.ConversionConcurrency)
        {
            Interlocked.Decrement(ref _conversionQueued);
            throw new RenderCapacityException("Conversion queue is full");
        }
        try { await _conversionSlots.WaitAsync(cancellationToken); }
        finally { Interlocked.Decrement(ref _conversionQueued); }
        try { return await ConvertAsync(request, cancellationToken); }
        finally { _conversionSlots.Release(); }
    }

    public async Task EnsureWarmWorkersAsync(CancellationToken cancellationToken)
    {
        int desired;
        lock (_workersGate)
        {
            desired = Math.Min(_options.Render.MinimumWarmWorkers - _idle.Count,
                _options.Render.MaxWorkers - _workerCount);
        }
        if (desired <= 0) { _ready = true; return; }

        var starts = Enumerable.Range(0, desired)
            .Select(async _ => (await AcquireWorkerAsync(cancellationToken)).Worker).ToArray();
        try { await Task.WhenAll(starts); }
        finally
        {
            foreach (var start in starts.Where(start => start.IsCompletedSuccessfully))
                ReleaseWorker(start.Result, true);
        }
        lock (_workersGate) _ready = _idle.Count >= Math.Min(_options.Render.MinimumWarmWorkers, _options.Render.MaxWorkers);
    }

    public RenderStatistics GetStatistics()
    {
        lock (_workersGate)
        {
            return new RenderStatistics
            {
                WorkerCount = _workerCount, IdleWorkerCount = _idle.Count, RunningJobs = Volatile.Read(ref _running),
                QueuedJobs = _renderGate.Queued + Math.Max(0, Volatile.Read(ref _conversionQueued)),
                CompletedJobs = Interlocked.Read(ref _completed), FailedJobs = Interlocked.Read(ref _failed),
                Capacity = _options.Render.MaxWorkers, QueueCapacity = _options.Render.QueueCapacity,
                ColdStarts = Interlocked.Read(ref _coldStarts), ReusedWorkers = Interlocked.Read(ref _reusedWorkers),
                CoalescedRequests = Interlocked.Read(ref _coalesced), Ready = _ready,
                Retries = Interlocked.Read(ref _retries), OutputBytes = Interlocked.Read(ref _outputBytes),
                InteractiveQueuedJobs = _renderGate.InteractiveQueued,
                BackgroundQueuedJobs = _renderGate.BackgroundQueued,
                ConversionQueuedJobs = Math.Max(0, Volatile.Read(ref _conversionQueued)),
                AverageQueueMilliseconds = Average(_queueMilliseconds, _completed + _failed),
                AverageRccMilliseconds = Average(_rccMilliseconds, _completed),
                AverageTotalMilliseconds = Average(_totalMilliseconds, _completed),
            };
        }
    }

    private static double Average(long total, long count) => count == 0 ? 0 : (double)Interlocked.Read(ref total) / count;

    public int CleanUpIdleWorkers()
    {
        List<RenderWorker> expired = [];
        lock (_workersGate)
        {
            foreach (var worker in _idle.OrderBy(worker => worker.LastUsedUtc).ToList())
            {
                var isDead = worker.Process.HasExited;
                var isExcessAndExpired = _idle.Count - expired.Count > _options.Render.MinimumWarmWorkers &&
                                         worker.LastUsedUtc.AddSeconds(_options.Render.IdleTtlSeconds) <= DateTime.UtcNow;
                if (isDead || isExcessAndExpired) expired.Add(worker);
            }
            _idle.RemoveAll(expired.Contains);
            _workerCount -= expired.Count;
        }
        foreach (var worker in expired) DisposeWorker(worker);
        return expired.Count;
    }

    private async Task<(RenderWorker Worker, bool Cold)> AcquireWorkerAsync(CancellationToken cancellationToken)
    {
        List<RenderWorker> dead = [];
        RenderWorker? reusable = null;
        lock (_workersGate)
        {
            while (_idle.Count > 0)
            {
                var index = _idle.Count - 1;
                var worker = _idle[index];
                _idle.RemoveAt(index);
                if (!worker.Process.HasExited)
                {
                    Interlocked.Increment(ref _reusedWorkers);
                    reusable = worker;
                    break;
                }
                _workerCount--;
                dead.Add(worker);
            }
            if (reusable == null) _workerCount++;
        }
        foreach (var worker in dead) DisposeWorker(worker);
        if (reusable != null) return (reusable, false);

        var port = _ports.Allocate(_options.Ports.Rcc);
        IManagedProcess? process = null;
        try
        {
            var executable = Path.Combine(_options.RccServiceRoot,
                $"RCCService{_options.Render.DefaultYear}", "RCCService.exe");
            var startWatch = Stopwatch.StartNew();
            process = _launcher.Start(executable, $"-console {port}", Path.GetDirectoryName(executable));
            await _readiness.WaitUntilAvailableAsync(port,
                TimeSpan.FromSeconds(_options.Processes.StartupTimeoutSeconds), cancellationToken);
            var soap = _soapFactory.Create(port);
            await soap.GetAllJobsAsync(cancellationToken);
            startWatch.Stop();
            Interlocked.Increment(ref _coldStarts);
            _logger.LogInformation("Started and probed RCC worker on port {Port} in {StartMilliseconds}ms",
                port, startWatch.Elapsed.TotalMilliseconds);
            return (new RenderWorker(port, process, soap), true);
        }
        catch
        {
            process?.KillTree(); process?.Dispose(); _ports.Release(port);
            lock (_workersGate) _workerCount--;
            throw;
        }
    }

    private void ReleaseWorker(RenderWorker worker, bool healthy)
    {
        var recycleAt = Math.Max(1, _options.Render.MaxReuseCount - 1);
        var retain = healthy && !worker.Process.HasExited && worker.UseCount < recycleAt;
        var pooled = false;
        lock (_workersGate)
        {
            if (retain && _idle.Count < _options.Render.MaximumIdleWorkers)
            {
                _idle.Add(worker);
                pooled = true;
            }
            else _workerCount--;
        }
        if (!pooled)
        {
            if (worker.UseCount >= recycleAt)
                _logger.LogInformation("Recycling RCC worker on port {Port} after {UseCount} renders",
                    worker.Port, worker.UseCount);
            DisposeWorker(worker);
            if (healthy && worker.UseCount >= recycleAt)
                _ = RestoreWarmCapacityAsync();
        }
    }

    private async Task RestoreWarmCapacityAsync()
    {
        try { await EnsureWarmWorkersAsync(CancellationToken.None); }
        catch (Exception ex) { _logger.LogWarning(ex, "Could not proactively replace a recycled RCC worker"); }
    }

    private async Task<RenderOutput> ConvertAsync(RenderRequest request, CancellationToken cancellationToken)
    {
        var bytes = Convert.FromBase64String(request.InputData!);
        var directory = Path.Combine(Path.GetTempPath(), "averia-render", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        var input = Path.Combine(directory, "input.rbxl");
        var output = Path.Combine(directory, "output.rbxl");
        try
        {
            await File.WriteAllBytesAsync(input, bytes, cancellationToken);
            using var process = Process.Start(new ProcessStartInfo(_options.Render.PlaceConverterPath,
                $"{(request.Kind == RenderKind.PlaceConversion ? "game" : "hat")} \"{output}\" \"{input}\"")
            { UseShellExecute = false, CreateNoWindow = true, WorkingDirectory = directory })
                ?? throw new RenderExecutionException("Could not start the place converter");
            await process.WaitForExitAsync(cancellationToken);
            if (process.ExitCode != 0 || !File.Exists(output))
                throw new RenderExecutionException($"Place converter exited with code {process.ExitCode}");
            var result = await File.ReadAllBytesAsync(output, cancellationToken);
            return new RenderOutput
            {
                JobId = Guid.NewGuid(), ContentType = "application/octet-stream", Data = result,
                WorkerState = "converter",
            };
        }
        finally { try { Directory.Delete(directory, true); } catch { } }
    }

    private void Validate(RenderRequest request)
    {
        if (request.Width <= 0 || request.Height <= 0 || request.Width > _options.Render.MaxDimension || request.Height > _options.Render.MaxDimension)
            throw new RenderValidationException($"Dimensions must be between 1 and {_options.Render.MaxDimension}");
        if (request.Kind is RenderKind.PlaceConversion or RenderKind.HatConversion)
        {
            if (string.IsNullOrWhiteSpace(request.InputData)) throw new RenderValidationException("inputData is required");
            try
            {
                if (Convert.FromBase64String(request.InputData).LongLength > _options.Render.MaxInputMegabytes * 1024L * 1024L)
                    throw new RenderValidationException("Input exceeds configured limit");
            }
            catch (FormatException) { throw new RenderValidationException("inputData must be valid base64"); }
        }
        else if (request.Kind is RenderKind.Avatar or RenderKind.AvatarHeadshot or RenderKind.Avatar3D)
        {
            if (request.UserId is null && request.Avatar is null && string.IsNullOrWhiteSpace(request.CharacterAppearanceUrl))
                throw new RenderValidationException("userId, avatar, or characterAppearanceUrl is required");
        }
        else if (request.Kind == RenderKind.Animation &&
                 (string.IsNullOrWhiteSpace(request.CharacterAppearanceUrl) || string.IsNullOrWhiteSpace(request.AnimationUrl)))
            throw new RenderValidationException("characterAppearanceUrl and animationUrl are required");
        else if (request.AssetId is null && string.IsNullOrWhiteSpace(request.AssetUrl) && string.IsNullOrWhiteSpace(request.AssetUrls))
            throw new RenderValidationException("assetId, assetUrl, or assetUrls is required");
    }

    private void DisposeWorker(RenderWorker worker)
    {
        _ports.Release(worker.Port);
        try { worker.Process.KillTree(); }
        finally { worker.Process.Dispose(); }
    }

    public void Dispose()
    {
        List<RenderWorker> workers;
        lock (_workersGate)
        {
            workers = [.. _idle];
            _idle.Clear();
            _workerCount = 0;
        }
        foreach (var worker in workers) DisposeWorker(worker);
        _conversionSlots.Dispose();
    }

    private sealed class RenderWorker(int port, IManagedProcess process, IRccSoapClient soap)
    {
        public int Port { get; } = port;
        public IManagedProcess Process { get; } = process;
        public IRccSoapClient Soap { get; } = soap;
        public int UseCount { get; set; }
        public DateTime LastUsedUtc { get; set; } = DateTime.UtcNow;
    }
}
