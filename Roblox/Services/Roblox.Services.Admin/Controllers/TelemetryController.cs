using Microsoft.AspNetCore.Mvc;
using Roblox.Models.Staff;
using Roblox.Services.Admin.Telemetry;
using Roblox.Web.Infrastructure.Admin;
using Roblox.Web.Infrastructure.Metadata;

namespace Roblox.Services.Admin.Controllers;

[ApiController]
[InternalServiceOnly]
[RequireRobloxSession]
[AdminStaffFilter]
[AdminTwoFactorFilter]
[Route("/v1/telemetry")]
public sealed class TelemetryController : ControllerBase
{
    private readonly ITelemetryQueryService _telemetry;

    public TelemetryController(ITelemetryQueryService telemetry)
    {
        _telemetry = telemetry;
    }

    [HttpGet("dashboard")]
    [AdminPermission(Access.ViewTelemetry)]
    [ProducesResponseType<TelemetryDashboardResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status503ServiceUnavailable)]
    public async Task<IActionResult> GetDashboard(
        [FromQuery] string range = "6h",
        [FromQuery] string service = "all",
        CancellationToken cancellationToken = default)
    {
        try
        {
            return Ok(await _telemetry.GetDashboardAsync(range, service, cancellationToken));
        }
        catch (ArgumentException exception)
        {
            return Problem(exception.Message, statusCode: StatusCodes.Status400BadRequest);
        }
        catch (TelemetryQueryException)
        {
            return Problem("Telemetry data is temporarily unavailable.", statusCode: StatusCodes.Status503ServiceUnavailable);
        }
    }
}
