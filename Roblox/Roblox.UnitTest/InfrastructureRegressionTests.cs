using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using Roblox.Services;
using Roblox.Services.DependencyInjection;
using Roblox.Web.Infrastructure.Services;
using Roblox.Website.Lib;
using Roblox.Website.Middleware;

namespace Roblox.UnitTest;

public class InfrastructureRegressionTests
{
    [Fact]
    public void ServiceProvider_DoesNotPoisonFutureResolutions_AfterConstructorFailure()
    {
        using var root = new ServiceCollection().BuildServiceProvider();
        Roblox.Services.ServiceProvider.Initialize(root);

        Assert.Throws<InvalidOperationException>(() => Roblox.Services.ServiceProvider.GetOrCreate<ThrowingTestService>());

        using var working = Roblox.Services.ServiceProvider.GetOrCreate<WorkingTestService>();
        Assert.NotNull(working);
    }

    [Fact]
    public void ServiceProvider_CopiesParentTransactionConnection()
    {
        using var connection = new NpgsqlConnection("Host=localhost;Username=user;Password=pass;Database=db");
        using var parent = new ParentTestService
        {
            transactionConnection = connection,
        };

        using var child = Roblox.Services.ServiceProvider.GetOrCreate<ChildTestService>(parent);
        Assert.Same(connection, child.transactionConnection);
    }

    [Fact]
    public void ServiceProvider_UsesActiveRequestServiceProvider()
    {
        var services = new ServiceCollection();
        services.AddScoped<GuidCarrier>();

        using var root = services.BuildServiceProvider();
        Roblox.Services.ServiceProvider.Initialize(root);

        using var scope = root.CreateScope();
        using var _ = Roblox.Services.ServiceProvider.BeginScope(scope.ServiceProvider);
        var carrier = scope.ServiceProvider.GetRequiredService<GuidCarrier>();

        using var resolved = Roblox.Services.ServiceProvider.GetOrCreate<ScopedDependencyTestService>();
        Assert.Same(carrier, resolved.Carrier);
    }

    [Fact]
    public void RobloxServiceAccessor_ResolvesServicesFromDependencyInjection()
    {
        var services = new ServiceCollection();
        services.AddRobloxServiceLayer();
        services.AddScoped<RobloxServiceAccessor>();

        using var root = services.BuildServiceProvider();
        using var scope = root.CreateScope();

        var accessor = scope.ServiceProvider.GetRequiredService<RobloxServiceAccessor>();
        Assert.NotNull(accessor.users);
    }

    [Fact]
    public void FileContentCache_RefreshesWhenFileChanges()
    {
        var cache = new FileContentCache();
        var path = Path.GetTempFileName();

        try
        {
            var firstWrite = DateTime.UtcNow.AddSeconds(-10);
            File.WriteAllText(path, "first");
            File.SetLastWriteTimeUtc(path, firstWrite);

            Assert.Equal("first", cache.ReadText(path));

            File.WriteAllText(path, "second");
            File.SetLastWriteTimeUtc(path, firstWrite.AddSeconds(5));

            Assert.Equal("second", cache.ReadText(path));
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public async Task TimerMiddleware_AppendsTimingHeaderFromDownstreamResults()
    {
        var middleware = new TimerMiddleware(ctx =>
        {
            ctx.Items[MiddlewareTimer.MiddlewareTimerKey] = new List<MiddlewareTimerResult>
            {
                new()
                {
                    name = "test",
                    elapsedMilliseconds = 42,
                },
            };

            return Task.CompletedTask;
        });

        var context = new DefaultHttpContext();
        context.Response.Body = new MemoryStream();

        await middleware.InvokeAsync(context);

        Assert.Equal("test=42", context.Response.Headers["x-timing"].ToString());
    }

    private sealed class ThrowingTestService : ServiceBase
    {
        public ThrowingTestService()
        {
            throw new InvalidOperationException("boom");
        }
    }

    private sealed class WorkingTestService : ServiceBase
    {
    }

    private sealed class ParentTestService : ServiceBase
    {
    }

    private sealed class ChildTestService : ServiceBase
    {
    }

    private sealed class GuidCarrier
    {
        public Guid Value { get; } = Guid.NewGuid();
    }

    private sealed class ScopedDependencyTestService : ServiceBase
    {
        public ScopedDependencyTestService(GuidCarrier carrier)
        {
            Carrier = carrier;
        }

        public GuidCarrier Carrier { get; }
    }
}
