namespace Averia.RccServiceArbiter.Processes;

public interface IRccProcessLauncher
{
    IManagedProcess Start(string fileName, string arguments, string? workingDirectory = null);
}

public interface IManagedProcess : IDisposable
{
    int? Id { get; }
    bool HasExited { get; }
    void KillTree();
}
