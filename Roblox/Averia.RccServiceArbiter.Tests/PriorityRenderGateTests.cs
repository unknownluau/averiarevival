using Averia.RccServiceArbiter.Rendering;
using Roblox.Rendering;
using Xunit;

namespace Averia.RccServiceArbiter.Tests;

public sealed class PriorityRenderGateTests
{
    [Fact]
    public async Task SustainedInteractiveWork_StillGrantsBackgroundAfterFourJobs()
    {
        var gate = new PriorityRenderGate(1, 10, 10);
        using var initial = await gate.WaitAsync(RenderPriority.Interactive, TestContext.Current.CancellationToken);
        var interactive = Enumerable.Range(0, 5)
            .Select(_ => gate.WaitAsync(RenderPriority.Interactive, TestContext.Current.CancellationToken).AsTask()).ToArray();
        var background = gate.WaitAsync(RenderPriority.Background, TestContext.Current.CancellationToken).AsTask();

        initial.Dispose();
        for (var index = 0; index < 4; index++)
        {
            using var lease = await interactive[index];
        }

        using var backgroundLease = await background;
        Assert.False(interactive[4].IsCompleted);
        backgroundLease.Dispose();
        using var finalLease = await interactive[4];
    }

    [Fact]
    public async Task FullPriorityQueue_RejectsWithoutWaiting()
    {
        var gate = new PriorityRenderGate(1, 1, 1);
        using var active = await gate.WaitAsync(RenderPriority.Interactive, TestContext.Current.CancellationToken);
        _ = gate.WaitAsync(RenderPriority.Background, TestContext.Current.CancellationToken);

        Assert.Throws<RenderCapacityException>(() =>
            gate.WaitAsync(RenderPriority.Background, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task CancelledWaiter_DetachesFromQueue()
    {
        var gate = new PriorityRenderGate(1, 1, 1);
        using var active = await gate.WaitAsync(RenderPriority.Interactive, TestContext.Current.CancellationToken);
        using var cancellation = new CancellationTokenSource();
        var waiting = gate.WaitAsync(RenderPriority.Background, cancellation.Token).AsTask();

        cancellation.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => waiting);
        Assert.Equal(0, gate.Queued);
    }
}
