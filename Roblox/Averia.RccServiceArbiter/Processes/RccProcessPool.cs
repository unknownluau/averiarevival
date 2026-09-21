using System.Collections.Concurrent;
using Averia.RccServiceArbiter.Configuration;
using Averia.RccServiceArbiter.Models;
using Averia.RccServiceArbiter.Rcc;
using Averia.RccServiceArbiter.Services;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Averia.RccServiceArbiter.Processes;

public sealed class RccProcessPool : IRccProcessPool
{
    private readonly SemaphoreSlim _gate = new(1, 100);
    private readonly ConcurrentDictionary<Guid, RccProcessHandle> _active = new();
    private readonly Dictionary<long, Queue<RccProcessHandle>> _idle = new();
    private readonly ArbiterOptions _options;
    private readonly IPortAllocator _ports;
    private readonly IRccProcessLauncher _launcher;
    private readonly IRccReadinessProbe _readinessProbe;
    private readonly IRccSoapClientFactory _soapClientFactory;
    private readonly IRccJsonPayloadFactory _payloadFactory;
    private readonly IArbiterPostStartQueue _postStartQueue;
    private readonly IArbiterClock _clock;
    private readonly ILogger<RccProcessPool> _logger;

    public RccProcessPool(
        IOptions<ArbiterOptions> options,
        IPortAllocator ports,
        IRccProcessLauncher launcher,
        IRccReadinessProbe readinessProbe,
        IRccSoapClientFactory soapClientFactory,
        IRccJsonPayloadFactory payloadFactory,
        IArbiterPostStartQueue postStartQueue,
        IArbiterClock clock,
        ILogger<RccProcessPool> logger)
    {
        _options = options.Value;
        _ports = ports;
        _launcher = launcher;
        _readinessProbe = readinessProbe;
        _soapClientFactory = soapClientFactory;
        _payloadFactory = payloadFactory;
        _postStartQueue = postStartQueue;
        _clock = clock;
        _logger = logger;
    }

    public async Task<StartGameServerResponse> StartGameServerAsync(StartGameServerRequest request, CancellationToken cancellationToken)
    {
        await _gate.WaitAsync(cancellationToken);
        RccProcessHandle? handle = null;
        var gameServerPort = 0;
        var proxyPort = 0;
        try
        {
            if (_active.ContainsKey(request.JobId))
            {
                throw new InvalidOperationException($"Game server {request.JobId} already exists");
            }

            CleanUpExpiredHandles();
            EnsureCapacity(request.Year);

            handle = await AcquireRccProcessAsync(request.Year, cancellationToken);
            gameServerPort = _ports.Allocate(_options.Ports.GameServer);
            proxyPort = _ports.Allocate(_options.Ports.Proxy);

            var quilkin = StartQuilkin(proxyPort, gameServerPort);
            var expirationUtc = _clock.UtcNow.AddSeconds(_options.Processes.JobExpirationSeconds);
            handle.AttachJob(request.JobId, gameServerPort, proxyPort, quilkin, expirationUtc);

            var job = new Job
            {
                Id = request.JobId.ToString(),
                Category = 1,
                Cores = 2,
                ExpirationInSeconds = _options.Processes.JobExpirationSeconds,
            };
            var script = RccScriptFactory.GameServer(_payloadFactory.CreateGameServerPayload(request, gameServerPort));
            await handle.SoapClient.OpenJobExAsync(job, script, cancellationToken);

            _active[request.JobId] = handle;
            //await _postStartQueue.EnqueueAsync(new ArbiterPostStartAction(request.JobId, request.Year), cancellationToken);

            return new StartGameServerResponse
            {
                JobId = request.JobId,
                RccPort = handle.RccPort,
                GameServerPort = gameServerPort,
                ProxyPort = proxyPort,
                RccProcessId = handle.RccProcessId,
                QuilkinProcessId = handle.QuilkinProcessId,
            };
        }
        catch
        {
            if (gameServerPort != 0)
            {
                _ports.Release(gameServerPort);
            }

            if (proxyPort != 0)
            {
                _ports.Release(proxyPort);
            }

            if (handle != null)
            {
                DisposeHandle(handle);
            }

            throw;
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task<bool> StopGameServerAsync(Guid jobId, CancellationToken cancellationToken)
    {
        RccProcessHandle? handle;
        await _gate.WaitAsync(cancellationToken);
        try
        {
            if (!_active.TryRemove(jobId, out handle))
            {
                return false;
            }
        }
        finally
        {
            _gate.Release();
        }

        try
        {
            await TryGracefulShutdownAsync(handle, jobId, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Graceful shutdown failed for job {JobId}; forcing kill", jobId);
        }

        await _gate.WaitAsync(CancellationToken.None);
        try
        {
            ReleaseGamePorts(handle);
            if (CanRecycle(handle))
            {
                handle.MarkIdle(_clock.UtcNow);
                EnqueueIdle(handle);
            }
            else
            {
                DisposeHandle(handle);
            }
            return true;
        }
        finally
        {
            _gate.Release();
        }
    }
    private async Task<bool> TryGracefulShutdownAsync(RccProcessHandle handle, Guid jobId, CancellationToken cancellationToken)
    {
        using var timeoutCts = new CancellationTokenSource(TimeSpan.FromSeconds(_options.Processes.ShutdownTimeoutSeconds));
        using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeoutCts.Token);

        try
        {
            if (handle.Year >= 2020)
            {
                await handle.SoapClient.ExecuteExAsync(jobId.ToString(), RccScriptFactory.Shutdown(), linkedCts.Token);
                return true;
            }

            await handle.SoapClient.CloseJobAsync(jobId.ToString(), linkedCts.Token);
            return true;
        }
        catch (OperationCanceledException) when (timeoutCts.IsCancellationRequested)
        {
            _logger.LogWarning("RCC close timed out for job {JobId}; forcing process kill", jobId);
            return false;
        }
    }

    public async Task<bool> EvictPlayerAsync(Guid jobId, long userId, int messageVersionId, CancellationToken cancellationToken)
    {
        if (!_active.TryGetValue(jobId, out var handle))
        {
            return false;
        }

        await handle.SoapClient.ExecuteExAsync(jobId.ToString(), RccScriptFactory.EvictPlayer(userId, messageVersionId), cancellationToken);
        return true;
    }

    public async Task<bool> SetFilteringEnabledAsync(Guid jobId, bool isEnabled, CancellationToken cancellationToken)
    {
        if (!_active.TryGetValue(jobId, out var handle))
        {
            return false;
        }

        await handle.SoapClient.ExecuteExAsync(jobId.ToString(), RccScriptFactory.SetFilteringEnabled(isEnabled), cancellationToken);
        return true;
    }

    public async Task<bool> RunGlobalMessageAsync(Guid jobId, string topic, CancellationToken cancellationToken)
    {
        if (!_active.TryGetValue(jobId, out var handle))
        {
            return false;
        }

        await handle.SoapClient.ExecuteExAsync(jobId.ToString(), RccScriptFactory.GlobalMessage(topic), cancellationToken);
        return true;
    }

    public async Task<int> CleanUpAsync(CancellationToken cancellationToken)
    {
        await _gate.WaitAsync(cancellationToken);
        try
        {
            return CleanUpExpiredHandles();
        }
        finally
        {
            _gate.Release();
        }
    }

    public ArbiterStatisticsResponse GetStatistics()
    {
        var servers = _active.ToDictionary(
            pair => pair.Key,
            pair => new ArbiterServerStatistics
            {
                Year = pair.Value.Year,
                RccPort = pair.Value.RccPort,
                GameServerPort = pair.Value.GameServerPort,
                ProxyPort = pair.Value.ProxyPort,
                RccProcessId = pair.Value.RccProcessId,
                QuilkinProcessId = pair.Value.QuilkinProcessId,
                UseCount = pair.Value.UseCount,
                ExpirationUtc = pair.Value.ExpirationUtc,
            });

        return new ArbiterStatisticsResponse
        {
            ServerCount = servers.Count,
            Servers = servers,
        };
    }

    private async Task<RccProcessHandle> AcquireRccProcessAsync(long year, CancellationToken cancellationToken)
    {
        if (_idle.TryGetValue(year, out var queue))
        {
            while (queue.TryDequeue(out var handle))
            {
                if (!handle.HasExited && handle.LastUsedUtc.AddSeconds(_options.Processes.IdleTtlSeconds) > _clock.UtcNow)
                {
                    return handle;
                }

                DisposeHandle(handle);
            }
        }

        var rccPort = _ports.Allocate(_options.Ports.Rcc);
        IManagedProcess? process = null;
        try
        {
            var exe = Path.Combine(_options.RccServiceRoot, $"RCCService{year}", "RCCService.exe");
            process = _launcher.Start(exe, $"-console {rccPort}", Path.GetDirectoryName(exe));
            await _readinessProbe.WaitUntilAvailableAsync(
                rccPort,
                TimeSpan.FromSeconds(_options.Processes.StartupTimeoutSeconds),
                cancellationToken);
            return new RccProcessHandle(year, rccPort, process, _soapClientFactory.Create(rccPort), _clock.UtcNow);
        }
        catch (Exception ex)
        {
            if (process != null)
            {
                try
                {
                    process.KillTree();
                }
                finally
                {
                    process.Dispose();
                }
            }

            _ports.Release(rccPort);
            _logger.LogWarning(ex, "Failed to start RCCService for year {Year}; killed startup process and released port {Port}", year, rccPort);
            throw;
        }
    }

    private void EnsureCapacity(long year)
    {
        if (_active.Count >= _options.Processes.MaxActiveProcesses)
        {
            _logger.LogWarning(
                "Rejecting RCC start for year {Year}: active process limit {Limit} reached",
                year,
                _options.Processes.MaxActiveProcesses);
            throw new InvalidOperationException($"Active RCC process limit reached ({_options.Processes.MaxActiveProcesses})");
        }

        var activeForYear = _active.Count(pair => pair.Value.Year == year);
        if (activeForYear >= _options.Processes.MaxActivePerYear)
        {
            _logger.LogWarning(
                "Rejecting RCC start for year {Year}: per-year active process limit {Limit} reached",
                year,
                _options.Processes.MaxActivePerYear);
            throw new InvalidOperationException($"Active RCC process limit reached for year {year} ({_options.Processes.MaxActivePerYear})");
        }
    }

    private int CleanUpExpiredHandles()
    {
        var removed = 0;
        foreach (var pair in _active.ToArray())
        {
            var handle = pair.Value;
            if (!handle.HasExited && handle.ExpirationUtc >= _clock.UtcNow)
            {
                continue;
            }

            if (_active.TryRemove(pair.Key, out var removedHandle))
            {
                ReleaseGamePorts(removedHandle);
                DisposeHandle(removedHandle);
                removed++;
            }
        }

        foreach (var year in _idle.Keys.ToList())
        {
            var queue = _idle[year];
            var kept = new Queue<RccProcessHandle>();
            while (queue.TryDequeue(out var handle))
            {
                if (handle.HasExited || handle.LastUsedUtc.AddSeconds(_options.Processes.IdleTtlSeconds) <= _clock.UtcNow)
                {
                    DisposeHandle(handle);
                    removed++;
                    continue;
                }

                kept.Enqueue(handle);
            }

            _idle[year] = kept;
        }

        return removed;
    }

    private IManagedProcess StartQuilkin(int proxyPort, int gameServerPort)
    {
        return _launcher.Start(
            _options.QuilkinPath,
            $"--no-admin proxy -p {proxyPort} -t 127.0.0.1:{gameServerPort}",
            Path.GetDirectoryName(_options.QuilkinPath));
    }

    private bool CanRecycle(RccProcessHandle handle)
    {
        return !handle.HasExited && handle.UseCount + 1 < _options.Processes.MaxReuseCount;
    }

    private void EnqueueIdle(RccProcessHandle handle)
    {
        if (!_idle.TryGetValue(handle.Year, out var queue))
        {
            queue = new Queue<RccProcessHandle>();
            _idle[handle.Year] = queue;
        }

        queue.Enqueue(handle);
        while (queue.Count > _options.Processes.ReservePerYear)
        {
            DisposeHandle(queue.Dequeue());
        }
    }

    private void ReleaseGamePorts(RccProcessHandle handle)
    {
        if (handle.GameServerPort != 0)
        {
            _ports.Release(handle.GameServerPort);
        }

        if (handle.ProxyPort != 0)
        {
            _ports.Release(handle.ProxyPort);
        }
    }

    private void DisposeHandle(RccProcessHandle handle)
    {
        _ports.Release(handle.RccPort);
        handle.Dispose();
    }
}
