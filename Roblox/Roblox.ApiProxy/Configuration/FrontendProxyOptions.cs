namespace Roblox.ApiProxy.Configuration;

public sealed class FrontendProxyOptions
{
    public const string SectionName = "FrontendProxy";

    public string DestinationPrefix { get; set; } = "http://127.0.0.1:3000/";

    public string[] PublicHosts { get; set; } =
    [
        "averia.lol",
        "www.averia.lol",
    ];
}
