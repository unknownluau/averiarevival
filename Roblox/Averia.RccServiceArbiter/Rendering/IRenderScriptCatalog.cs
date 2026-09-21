using Averia.RccServiceArbiter.Rcc;
using Roblox.Rendering;

namespace Averia.RccServiceArbiter.Rendering;

public interface IRenderScriptCatalog
{
    ScriptExecution Create(RenderRequest request);
}

