using Averia.RccServiceArbiter.Rcc;

namespace Averia.RccServiceArbiter.Processes;

public sealed class RccProcessHandle : IDisposable
{
    private IManagedProcess? _quilkinProcess;
    private bool _disposed;

    public RccProcessHandle(long year, int rccPort, IManagedProcess rccProcess, IRccSoapClient soapClient, DateTime nowUtc)
    {
        Year = year;
        RccPort = rccPort;
        RccProcess = rccProcess;
        SoapClient = soapClient;
        LastUsedUtc = nowUtc;
    }

    public long Year { get; }
    public int RccPort { get; }
    public int GameServerPort { get; private set; }
    public int ProxyPort { get; private set; }
    public IManagedProcess RccProcess { get; }
    public IRccSoapClient SoapClient { get; }
    public Guid? JobId { get; private set; }
    public int UseCount { get; private set; }
    public DateTime ExpirationUtc { get; private set; }
    public DateTime LastUsedUtc { get; private set; }
    public int? RccProcessId => RccProcess.Id;
    public int? QuilkinProcessId => _quilkinProcess?.Id;
    public bool HasExited => RccProcess.HasExited || (_quilkinProcess?.HasExited ?? false);
    public bool IsIdle => JobId == null;

    public void AttachJob(Guid jobId, int gameServerPort, int proxyPort, IManagedProcess quilkinProcess, DateTime expirationUtc)
    {
        JobId = jobId;
        GameServerPort = gameServerPort;
        ProxyPort = proxyPort;
        _quilkinProcess = quilkinProcess;
        ExpirationUtc = expirationUtc;
    }

    public void MarkIdle(DateTime nowUtc)
    {
        JobId = null;
        GameServerPort = 0;
        ProxyPort = 0;
        UseCount++;
        LastUsedUtc = nowUtc;
        KillQuilkin();
    }

    public void KillQuilkin()
    {
        if (_quilkinProcess == null)
        {
            return;
        }

        try
        {
            _quilkinProcess.KillTree();
        }
        finally
        {
            _quilkinProcess.Dispose();
            _quilkinProcess = null;
        }
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        KillQuilkin();
        try
        {
            RccProcess.KillTree();
        }
        finally
        {
            RccProcess.Dispose();
            _disposed = true;
        }
    }
}
