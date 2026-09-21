using System.Net.Sockets;

namespace Averia.RccServiceArbiter.Processes;

public sealed class TcpRccReadinessProbe : IRccReadinessProbe
{
    public async Task WaitUntilAvailableAsync(int port, TimeSpan timeout, CancellationToken cancellationToken)
    {
        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutCts.CancelAfter(timeout);

        while (!timeoutCts.IsCancellationRequested)
        {
            try
            {
                using var client = new TcpClient();
                await client.ConnectAsync("127.0.0.1", port, timeoutCts.Token);
                return;
            }
            catch when (!timeoutCts.IsCancellationRequested)
            {
                await Task.Delay(TimeSpan.FromMilliseconds(50), timeoutCts.Token);
            }
        }

        throw new TimeoutException($"RCCService did not open port {port} within {timeout.TotalSeconds:0} seconds");
    }
}
