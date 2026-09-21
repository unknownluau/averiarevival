using System.Threading.Channels;

namespace Averia.RccServiceArbiter.Services;

public sealed record ArbiterPostStartAction(Guid JobId, long Year);

public interface IArbiterPostStartQueue
{
    ValueTask EnqueueAsync(ArbiterPostStartAction action, CancellationToken cancellationToken);
    IAsyncEnumerable<ArbiterPostStartAction> ReadAllAsync(CancellationToken cancellationToken);
}

public sealed class ArbiterPostStartQueue : IArbiterPostStartQueue
{
    private readonly Channel<ArbiterPostStartAction> _channel = Channel.CreateUnbounded<ArbiterPostStartAction>();

    public ValueTask EnqueueAsync(ArbiterPostStartAction action, CancellationToken cancellationToken)
    {
        return _channel.Writer.WriteAsync(action, cancellationToken);
    }

    public IAsyncEnumerable<ArbiterPostStartAction> ReadAllAsync(CancellationToken cancellationToken)
    {
        return _channel.Reader.ReadAllAsync(cancellationToken);
    }
}
