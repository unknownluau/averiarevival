using MVC = Microsoft.AspNetCore.Mvc;
using CsvHelper;
using System.Xml;
namespace Roblox.Website.Controllers
{

    [MVC.ApiController]
    [MVC.Route("/")]
    public class Policy : ControllerBase
    {
        [HttpGetBypass("v1/player-policies-client")]
        public dynamic GetPlayerPolicies()
        {
            return new
            {
                isSubjectToChinaPolicies = false,
                arePaidRandomItemsRestricted = false,
                isPaidItemTradingAllowed = true,
                allowedExternalLinkReferences = new List<string>
                {
                    "Discord",
                    "YouTube",
                }
            };
        }

    }
}