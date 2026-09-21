using Microsoft.AspNetCore.Mvc;

namespace Roblox.Website.Controllers.RobloxApi;

[ApiController]
[Route("/")]
public class Develop: ControllerBase
{
    [HttpGetBypass("/v1/places/{placeId:long}/symbolic-links")]
    public IActionResult GetPlaceSymbolicLinks(long placeId, string? sortOrder = "Asc", int? limit = 50)
    {
        return Ok(new
        {
            previousPageCursor = (string?)null,
            nextPageCursor = (string?)null,
            data = Array.Empty<string>()
        });
    }
}