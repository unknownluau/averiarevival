namespace Averia.RccServiceArbiter.Processes;

public interface IArbiterClock
{
    DateTime UtcNow { get; }
}

public sealed class SystemArbiterClock : IArbiterClock
{
    public DateTime UtcNow => DateTime.UtcNow;
}
