using Microsoft.Extensions.Configuration;

namespace Roblox.Services.Data;

public static class DataServiceConfiguration
{
    public static void Initialize(IConfiguration configuration)
    {
        Roblox.Configuration.IsCdnEnabled = configuration.GetValue<bool>("IsCdnEnabled");
        Roblox.Configuration.CdnBaseUrl = configuration["CdnBaseUrl"] ?? string.Empty;
        Roblox.Configuration.AssetDirectory = configuration["Directories:Asset"] ?? string.Empty;
        Roblox.Configuration.StorageDirectory = configuration["Directories:Storage"] ?? string.Empty;
        Roblox.Configuration.R2AccountId = configuration["CloudflareR2:AccountId"] ?? string.Empty;
        Roblox.Configuration.R2AccessKey = configuration["CloudflareR2:AccessKey"] ?? string.Empty;
        Roblox.Configuration.R2SecretKey = configuration["CloudflareR2:SecretKey"] ?? string.Empty;
        Roblox.Configuration.R2BucketName = configuration["CloudflareR2:BucketName"] ?? string.Empty;
        Roblox.Configuration.AssetValidationServiceUrl = configuration["AssetValidation:BaseUrl"] ?? string.Empty;
        Roblox.Configuration.AssetValidationServiceAuthorization = configuration["AssetValidation:Authorization"] ?? string.Empty;
    }
}
