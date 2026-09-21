namespace Roblox.ApiProxy.Configuration;

public sealed class AdminApiOptions
{
    public const string SectionName = "AdminApi";

    public string PublicBaseUrl { get; set; } = "https://admin.averia.lol/v1/";

    public string[] CorsAllowedOrigins { get; set; } =
    [
        "https://www.averia.lol",
        "https://averia.lol",
        "http://localhost:3000",
        "http://localhost:5200",
    ];
}
