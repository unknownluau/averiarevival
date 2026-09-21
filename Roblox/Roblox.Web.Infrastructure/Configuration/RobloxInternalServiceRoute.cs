namespace Roblox.Web.Infrastructure.Configuration;

public class RobloxInternalServiceRoute
{
    public List<string> Hosts { get; set; } = new();
    public List<string> PathPrefixes { get; set; } = new();
}
