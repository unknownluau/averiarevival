namespace Roblox.Web.Infrastructure.Http;

public interface IRobloxRequestContextAccessor
{
    RobloxRequestContext Current { get; }
    void SetCurrent(RobloxRequestContext context);
}
