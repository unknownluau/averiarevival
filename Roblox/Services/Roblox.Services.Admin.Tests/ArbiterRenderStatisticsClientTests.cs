using System.Net;
using System.Text;
using Microsoft.Extensions.Logging.Abstractions;
using Roblox.Services.Admin.Telemetry;

namespace Roblox.Services.Admin.Tests;

public sealed class ArbiterRenderStatisticsClientTests
{
    [Fact]
    public async Task Statistics_ParsesBoundedPoolSnapshot()
    {
        using var http = new HttpClient(new Handler(HttpStatusCode.OK, """
            {"WorkerCount":3,"IdleWorkerCount":2,"RunningJobs":1,"InteractiveQueuedJobs":4,
             "BackgroundQueuedJobs":2,"ConversionQueuedJobs":1,"ColdStarts":3,"ReusedWorkers":20,
             "Retries":1,"CoalescedRequests":5,"AverageQueueMilliseconds":12.5,
             "AverageRccMilliseconds":150.0,"AverageTotalMilliseconds":168.0,"Ready":true}
            """)) { BaseAddress = new Uri("http://arbiter.test/") };
        var client = new ArbiterRenderStatisticsClient(http, NullLogger<ArbiterRenderStatisticsClient>.Instance);

        var result = await client.GetAsync(TestContext.Current.CancellationToken);

        Assert.NotNull(result);
        Assert.Equal(3, result.WorkerCount);
        Assert.Equal(150, result.AverageRccMilliseconds);
        Assert.True(result.Ready);
    }

    [Fact]
    public async Task Statistics_UpstreamFailure_IsOptional()
    {
        using var http = new HttpClient(new Handler(HttpStatusCode.ServiceUnavailable, ""))
            { BaseAddress = new Uri("http://arbiter.test/") };
        var client = new ArbiterRenderStatisticsClient(http, NullLogger<ArbiterRenderStatisticsClient>.Instance);

        Assert.Null(await client.GetAsync(TestContext.Current.CancellationToken));
    }

    private sealed class Handler(HttpStatusCode status, string body) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromResult(new HttpResponseMessage(status)
                { Content = new StringContent(body, Encoding.UTF8, "application/json") });
    }
}
