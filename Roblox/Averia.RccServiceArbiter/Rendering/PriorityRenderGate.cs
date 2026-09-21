using Roblox.Rendering;

namespace Averia.RccServiceArbiter.Rendering;

internal sealed class PriorityRenderGate
{
    private readonly object _gate = new();
    private readonly LinkedList<Waiter> _interactive = [];
    private readonly LinkedList<Waiter> _background = [];
    private int _available;
    private int _interactiveStreak;
    private readonly int _interactiveCapacity;
    private readonly int _backgroundCapacity;

    public PriorityRenderGate(int permits, int interactiveCapacity, int backgroundCapacity)
    {
        _available = permits;
        _interactiveCapacity = interactiveCapacity;
        _backgroundCapacity = backgroundCapacity;
    }

    public int Queued
    {
        get { lock (_gate) return _interactive.Count + _background.Count; }
    }
    public int InteractiveQueued { get { lock (_gate) return _interactive.Count; } }
    public int BackgroundQueued { get { lock (_gate) return _background.Count; } }

    public ValueTask<IDisposable> WaitAsync(RenderPriority priority, CancellationToken cancellationToken)
    {
        lock (_gate)
        {
            if (_available > 0)
            {
                _available--;
                return ValueTask.FromResult<IDisposable>(new Lease(this));
            }

            var queue = priority == RenderPriority.Background ? _background : _interactive;
            var capacity = priority == RenderPriority.Background ? _backgroundCapacity : _interactiveCapacity;
            if (queue.Count >= capacity) throw new RenderCapacityException("Render queue is full");

            var waiter = new Waiter(this, queue, cancellationToken);
            waiter.Node = queue.AddLast(waiter);
            waiter.RegisterCancellation();
            return new ValueTask<IDisposable>(waiter.Completion.Task);
        }
    }

    private void Release()
    {
        Waiter? next = null;
        lock (_gate)
        {
            if (_interactive.Count > 0 && (_interactiveStreak < 4 || _background.Count == 0))
            {
                next = TakeFirst(_interactive);
                _interactiveStreak++;
            }
            else if (_background.Count > 0)
            {
                next = TakeFirst(_background);
                _interactiveStreak = 0;
            }
            else if (_interactive.Count > 0)
            {
                next = TakeFirst(_interactive);
                _interactiveStreak = 1;
            }
            else
            {
                _available++;
            }
        }
        next?.Grant();
    }

    private static Waiter TakeFirst(LinkedList<Waiter> queue)
    {
        var waiter = queue.First!.Value;
        queue.RemoveFirst();
        waiter.Node = null;
        return waiter;
    }

    private sealed class Lease(PriorityRenderGate owner) : IDisposable
    {
        private PriorityRenderGate? _owner = owner;
        public void Dispose() => Interlocked.Exchange(ref _owner, null)?.Release();
    }

    private sealed class Waiter(PriorityRenderGate owner, LinkedList<Waiter> queue, CancellationToken token)
    {
        public TaskCompletionSource<IDisposable> Completion { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public LinkedListNode<Waiter>? Node { get; set; }
        private CancellationTokenRegistration _registration;

        public void RegisterCancellation()
        {
            if (token.CanBeCanceled) _registration = token.Register(static state => ((Waiter)state!).Cancel(), this);
        }

        public void Grant()
        {
            _registration.Dispose();
            Completion.TrySetResult(new Lease(owner));
        }

        private void Cancel()
        {
            lock (owner._gate)
            {
                if (Node == null) return;
                queue.Remove(Node);
                Node = null;
            }
            Completion.TrySetCanceled(token);
        }
    }
}
