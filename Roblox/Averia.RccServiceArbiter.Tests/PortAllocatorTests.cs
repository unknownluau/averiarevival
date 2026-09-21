using Averia.RccServiceArbiter.Configuration;
using Averia.RccServiceArbiter.Processes;
using Microsoft.Extensions.Options;
using Xunit;

namespace Averia.RccServiceArbiter.Tests;

public sealed class PortAllocatorTests
{
    [Fact]
    public void Allocate_DoesNotReturnPortHeldByAllocator()
    {
        var allocator = CreateAllocator();
        var range = new PortRange { Start = 62000, End = 62010 };

        var first = allocator.Allocate(range);
        var second = allocator.Allocate(range);

        Assert.NotEqual(first, second);
    }

    [Fact]
    public void Release_HoldsPortInRecentlyUsedWindow()
    {
        var allocator = CreateAllocator();
        var range = new PortRange { Start = 62100, End = 62102 };

        var first = allocator.Allocate(range);
        allocator.Release(first);
        var second = allocator.Allocate(range);

        Assert.NotEqual(first, second);
    }

    private static PortAllocator CreateAllocator()
    {
        return new PortAllocator(
            new FakeClock(),
            Options.Create(new ArbiterOptions
            {
                Ports = new ArbiterPortOptions
                {
                    RecentlyUsedHoldSeconds = 60,
                },
            }));
    }

    private sealed class FakeClock : IArbiterClock
    {
        public DateTime UtcNow { get; } = new(2026, 6, 14, 0, 0, 0, DateTimeKind.Utc);
    }
}
