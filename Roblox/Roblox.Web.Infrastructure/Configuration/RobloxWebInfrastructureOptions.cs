namespace Roblox.Web.Infrastructure.Configuration;

public class RobloxWebInfrastructureOptions
{
    public string? Authorization { get; set; }
    public string? RccAuthorization { get; set; }
    public string? SessionJwtKey { get; set; }
    public List<string> InternalServiceHosts { get; set; } = new();
    public List<RobloxInternalServiceRoute> InternalServiceRoutes { get; set; } = new();
}
