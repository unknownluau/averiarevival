using System.Net.NetworkInformation;
using Averia.RccServiceArbiter.Configuration;
using Microsoft.Extensions.Options;

namespace Averia.RccServiceArbiter.Processes;

public sealed class PortAllocator : IPortAllocator
{
    private readonly object _sync = new();
    private readonly HashSet<int> _allocated = new();
    private readonly Dictionary<int, DateTime> _recentlyUsedUntil = new();
    private readonly Random _random = new();
    private readonly IArbiterClock _clock;
    private readonly ArbiterOptions _options;

    public PortAllocator(IArbiterClock clock, IOptions<ArbiterOptions> options)
    {
        _clock = clock;
        _options = options.Value;
    }

    public int Allocate(PortRange range)
    {
        if (range.End <= range.Start)
        {
            throw new InvalidOperationException($"Invalid port range {range.Start}-{range.End}");
        }

        lock (_sync)
        {
            PruneRecentlyUsed();
            var listeners = IPGlobalProperties.GetIPGlobalProperties()
                .GetActiveTcpListeners()
                .Select(endpoint => endpoint.Port)
                .ToHashSet();

            var attempts = Math.Max(100, (range.End - range.Start) * 2);
            for (var i = 0; i < attempts; i++)
            {
                var port = _random.Next(range.Start, range.End);
                if (_allocated.Contains(port) || _recentlyUsedUntil.ContainsKey(port) || listeners.Contains(port))
                {
                    continue;
                }

                _allocated.Add(port);
                return port;
            }

            throw new InvalidOperationException($"Failed to allocate a free port in {range.Start}-{range.End}");
        }
    }

    public void Release(int port)
    {
        lock (_sync)
        {
            _allocated.Remove(port);
            if (_options.Ports.RecentlyUsedHoldSeconds > 0)
            {
                _recentlyUsedUntil[port] = _clock.UtcNow.AddSeconds(_options.Ports.RecentlyUsedHoldSeconds);
            }
        }
    }

    private void PruneRecentlyUsed()
    {
        var now = _clock.UtcNow;
        foreach (var port in _recentlyUsedUntil.Where(pair => pair.Value <= now).Select(pair => pair.Key).ToList())
        {
            _recentlyUsedUntil.Remove(port);
        }
    }
}
