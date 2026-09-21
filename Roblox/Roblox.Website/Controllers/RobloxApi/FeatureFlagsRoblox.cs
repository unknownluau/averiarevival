using MVC = Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc;
using Roblox.Exceptions;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Formatters;
using Roblox.Web.Infrastructure.Metadata;

namespace Roblox.Website.Controllers
{
    [MVC.ApiController]
    [MVC.Route("/")]
    public class FeatureFlagsRoblox : ControllerBase
    {
        [HttpPostBypass("v2/settings/application")]
        [HttpGetBypass("v2/settings/application")]
        [HttpPostBypass("v1/settings/application")]
        [HttpGetBypass("v1/settings/application")]
        [AllowRobloxAnonymous]
        public MVC.ActionResult<dynamic> GetApplicationSettingsModern(string applicationName)
        {
            return Content(GetFeatureFlags(applicationName), "application/json");
        }

        // For modern clients
        private static readonly HashSet<string> applicationNames = new HashSet<string>
        {
            "RCCService2019",
            "PCDesktopClient2019",
            "RCCService2020",
            "PCStudioApp",
            "PCStudio221",
            "PCStudio223",
            "RCCService2021",
            "RCCServiceGDASTGWG72713", // 2021 Too
            "PCDesktopClient",
            "PCDesktopClient2021",
            "PCDesktopCli223",
            "AndroidApp",
            "iOSApp"
        };

        private string GetFeatureFlags(string type)
        {
            if (!applicationNames.Contains(type))
                throw new BadRequestException(1, $"Invalid application name: {type}");

            if (type == "PCStudio221")
                type = "PCDesktopClient2021";

            // temp
            if (type == "RCCServiceGDASTGWG72713")
                type = "RCCService2021";

            string featureFlags = Path.Join(Configuration.JsonDataDirectory, $"{type}.json");
            
            // Also should never happen, but just in case
            if (!System.IO.File.Exists(featureFlags))
                throw new BadRequestException(0, $"Feature flags not found for {type}");

            return FileContentCache.ReadText(featureFlags);
        }
    }
}
