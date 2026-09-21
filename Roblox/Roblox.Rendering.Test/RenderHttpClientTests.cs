using System;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace Roblox.Rendering.Test;

public sealed class RenderHttpClientTests
{
    [Fact]
    public async Task CommandHandler_UsesTypedHttpContractAndReturnsDecodedStream()
    {
        var handler = new RecordingHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new ByteArrayContent("png"u8.ToArray()) { Headers = { ContentType = new("image/png") } },
        });
        RenderHttpClient.Configure(new HttpClient(handler) { BaseAddress = new Uri("http://arbiter.test/") });

        await using var stream = await CommandHandler.RequestAssetGame(139, 640, 360, TestContext.Current.CancellationToken);
        using var reader = new StreamReader(stream);
        Assert.Equal("png", await reader.ReadToEndAsync(TestContext.Current.CancellationToken));
        Assert.Equal(HttpMethod.Post, handler.Request!.Method); Assert.Equal("/render/v2", handler.Request.RequestUri!.AbsolutePath);
        var request = JsonSerializer.Deserialize<RenderRequest>(handler.Body!, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        Assert.NotNull(request); Assert.Equal(RenderKind.Place, request.Kind); Assert.Equal(139, request.AssetId);
        Assert.Equal(640, request.Width); Assert.Equal(360, request.Height);
    }

    [Fact]
    public async Task ErrorResponse_ProducesHttpRequestExceptionWithStatusAndMessage()
    {
        var handler = new RecordingHandler(_ => new HttpResponseMessage(HttpStatusCode.TooManyRequests)
        {
            Content = new StringContent("{\"errors\":[{\"code\":0,\"message\":\"queue full\"}]}", Encoding.UTF8, "application/json"),
        });
        RenderHttpClient.Configure(new HttpClient(handler) { BaseAddress = new Uri("http://arbiter.test/") });
        var exception = await Assert.ThrowsAsync<HttpRequestException>(() => CommandHandler.RequestAssetThumbnail(1, TestContext.Current.CancellationToken));
        Assert.Equal(HttpStatusCode.TooManyRequests, exception.StatusCode); Assert.Contains("queue full", exception.Message);
    }

    [Fact]
    public async Task PlayerThumbnail_PropagatesR6RigType()
    {
        var handler = new RecordingHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new ByteArrayContent("png"u8.ToArray()) { Headers = { ContentType = new("image/png") } },
        });
        RenderHttpClient.Configure(new HttpClient(handler) { BaseAddress = new Uri("http://arbiter.test/") });
        _ = await RenderingHandler.RequestPlayerThumbnail(123, AvatarRigType.R6, TestContext.Current.CancellationToken);
        var request = JsonSerializer.Deserialize<RenderRequest>(handler.Body!, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        Assert.NotNull(request); Assert.Equal(AvatarRigType.R6, request.AvatarRigType);
        Assert.Equal(704, request.Width); Assert.Equal(704, request.Height);
    }

    [Fact]
    public async Task BinaryEndpoint_NotFound_FallsBackToLegacyEndpoint()
    {
        var calls = 0;
        var handler = new RecordingHandler(request =>
        {
            calls++;
            if (request.RequestUri!.AbsolutePath == "/render/v2")
                return new HttpResponseMessage(HttpStatusCode.NotFound)
                { Content = new StringContent("{\"errors\":[{\"code\":0,\"message\":\"not found\"}]}") };
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(JsonSerializer.Serialize(new RenderResult
                    { JobId = Guid.NewGuid(), ContentType = "image/png", Data = "cG5n", DependencyUrls = [] }), Encoding.UTF8, "application/json"),
            };
        });
        RenderHttpClient.Configure(new HttpClient(handler) { BaseAddress = new Uri("http://arbiter.test/") });

        await using var stream = await CommandHandler.RequestAssetThumbnail(1, TestContext.Current.CancellationToken);

        Assert.Equal(2, calls);
        Assert.Equal("/render", handler.Request!.RequestUri!.AbsolutePath);
    }

    private sealed class RecordingHandler(Func<HttpRequestMessage, HttpResponseMessage> response) : HttpMessageHandler
    {
        public HttpRequestMessage? Request { get; private set; }
        public string? Body { get; private set; }
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        { Request = request; Body = request.Content == null ? null : await request.Content.ReadAsStringAsync(cancellationToken); return response(request); }
    }
}
