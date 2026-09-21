using Roblox.Web.Infrastructure.Controllers;
namespace Roblox.Website.Controllers
{
    public class ControllerBase : RobloxControllerBase
    {
        public static new string GetRequesterIpRaw(HttpContext ctx)
        {
            return RobloxControllerBase.GetRequesterIpRaw(ctx);
        }

        public static new Task InitializeIpHashSetupAsync()
        {
            return RobloxControllerBase.InitializeIpHashSetupAsync();
        }

        public static new ulong ConvertFromIpAddressToInteger(string ipAddress)
        {
            return RobloxControllerBase.ConvertFromIpAddressToInteger(ipAddress);
        }

        public static new string GetIP(string trueIp, string? salt = null)
        {
            return RobloxControllerBase.GetIP(trueIp, salt);
        }
    }
}

