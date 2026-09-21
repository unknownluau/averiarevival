using Averia.RccServiceArbiter.Configuration;
using Averia.RccServiceArbiter.Processes;
using Averia.RccServiceArbiter.Rcc;
using Averia.RccServiceArbiter.Rendering;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Roblox.Rendering;
using Xunit;

namespace Averia.RccServiceArbiter.Tests;

public sealed class RenderServiceCoalescingTests
{
    [Fact]
    public async Task CancelledWaiter_DoesNotLeaveCompletedInflightEntryBehind()
    {
        var soap = new ControlledSoapClient();
        using var service = new RenderService(Options.Create(new ArbiterOptions
        {
            RccServiceRoot = ".",
            Processes = new ArbiterProcessOptions { StartupTimeoutSeconds = 1 },
            Render = new ArbiterRenderOptions { MaxWorkers = 1, MaximumIdleWorkers = 1, JobTimeoutSeconds = 5 },
        }), new FakePorts(), new FakeLauncher(), new FakeReadiness(), new FakeSoapFactory(soap),
            new FakeScripts(), NullLogger<RenderService>.Instance);
        var request = new RenderRequest
        {
            Kind = RenderKind.Hat, AssetId = 1, WorkKey = "asset-version:1", Priority = RenderPriority.Background,
        };
        using var cancellation = new CancellationTokenSource();

        var abandoned = service.RenderAsync(request, cancellation.Token);
        await soap.FirstCallStarted.Task.WaitAsync(TestContext.Current.CancellationToken);
        cancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => abandoned);

        soap.CompleteFirst();
        for (var attempt = 0; attempt < 100 && service.GetStatistics().CompletedJobs == 0; attempt++)
            await Task.Delay(10, TestContext.Current.CancellationToken);

        var retry = await service.RenderAsync(request, TestContext.Current.CancellationToken);

        Assert.Equal("png"u8.ToArray(), retry.Data);
        Assert.Equal(2, soap.RenderCalls);
    }

    private sealed class FakePorts : IPortAllocator
    {
        public int Allocate(PortRange range) => 45000;
        public void Release(int port) { }
    }

    private sealed class FakeLauncher : IRccProcessLauncher
    {
        public IManagedProcess Start(string fileName, string arguments, string? workingDirectory = null) => new FakeProcess();
    }

    private sealed class FakeProcess : IManagedProcess
    {
        public int? Id => 1;
        public bool HasExited => false;
        public void KillTree() { }
        public void Dispose() { }
    }

    private sealed class FakeReadiness : IRccReadinessProbe
    {
        public Task WaitUntilAvailableAsync(int port, TimeSpan timeout, CancellationToken cancellationToken) => Task.CompletedTask;
    }

    private sealed class FakeSoapFactory(IRccSoapClient soap) : IRccSoapClientFactory
    {
        public IRccSoapClient Create(int port) => soap;
    }

    private sealed class FakeScripts : IRenderScriptCatalog
    {
        public ScriptExecution Create(RenderRequest request) => new() { Name = "test", Script = "{}" };
    }

    private sealed class ControlledSoapClient : IRccSoapClient
    {
        private readonly TaskCompletionSource<RccRenderResponse> _first = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource FirstCallStarted { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public int RenderCalls { get; private set; }
        public void CompleteFirst() => _first.TrySetResult(new RccRenderResponse("png"u8.ToArray(), []));

        public Task<RccRenderResponse> BatchRenderAsync(Job job, ScriptExecution script, bool modern, bool jsonOutput,
            int maximumBytes, CancellationToken cancellationToken)
        {
            RenderCalls++;
            if (RenderCalls == 1)
            {
                FirstCallStarted.TrySetResult();
                return _first.Task;
            }
            return Task.FromResult(new RccRenderResponse("png"u8.ToArray(), []));
        }

        public Task OpenJobExAsync(Job job, ScriptExecution script, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<IReadOnlyList<LuaValue>> BatchJobAsync(Job job, ScriptExecution script, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<IReadOnlyList<LuaValue>> BatchJobExAsync(Job job, ScriptExecution script, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task ExecuteExAsync(string jobId, ScriptExecution script, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task CloseJobAsync(string jobId, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<IReadOnlyList<RccServiceJob>> GetAllJobsAsync(CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<RccServiceJob>>([]);
    }
}
