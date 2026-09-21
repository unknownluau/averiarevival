using System.Threading;
using Microsoft.Extensions.DependencyInjection;

namespace Roblox.Services;

public static class ServiceProvider
{
    private static readonly AsyncLocal<IServiceProvider?> CurrentProvider = new();
    private static IServiceProvider? _rootProvider;

    public static void Initialize(IServiceProvider serviceProvider)
    {
        _rootProvider = serviceProvider;
    }

    public static IDisposable BeginScope(IServiceProvider serviceProvider)
    {
        return new RequestScopeCookie(serviceProvider);
    }

    public static T GetOrCreate<T>(ServiceBase? parent = null) where T : ServiceBase, IDisposable
    {
        var provider = CurrentProvider.Value ?? _rootProvider;
        if (provider != null)
        {
            var service = parent == null
                ? ResolveOrCreate<T>(provider)
                : ActivatorUtilities.CreateInstance<T>(provider);
            if (parent != null)
            {
                service.transactionConnection = parent.transactionConnection;
            }

            return service;
        }

        var fallback = Activator.CreateInstance<T>();
        if (parent != null)
        {
            fallback.transactionConnection = parent.transactionConnection;
        }

        return fallback;
    }

    private static T ResolveOrCreate<T>(IServiceProvider provider) where T : ServiceBase, IDisposable
    {
        if (provider.GetService<T>() is T registered)
        {
            return registered;
        }

        return ActivatorUtilities.CreateInstance<T>(provider);
    }

    private sealed class RequestScopeCookie : IDisposable
    {
        private readonly IServiceProvider? _previous;
        private bool _disposed;

        public RequestScopeCookie(IServiceProvider serviceProvider)
        {
            _previous = CurrentProvider.Value;
            CurrentProvider.Value = serviceProvider;
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            CurrentProvider.Value = _previous;
        }
    }
}
