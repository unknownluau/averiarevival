using Averia.RccServiceArbiter.Configuration;

namespace Averia.RccServiceArbiter.Processes;

public interface IPortAllocator
{
    int Allocate(PortRange range);
    void Release(int port);
}
