using System.Reflection;
using Averia.RccServiceArbiter.Models;
using Averia.RccServiceArbiter.Processes;
using Microsoft.AspNetCore.Mvc;
using Roblox.Web.Infrastructure.Metadata;

namespace Averia.RccServiceArbiter.Controllers;

[ApiController]
[InternalServiceOnly]
[Route("")]
public sealed class ArbiterController : ControllerBase
{
    private readonly IRccProcessPool _pool;

    public ArbiterController(IRccProcessPool pool)
    {
        _pool = pool;
    }

    [HttpGet("version")]
    public IActionResult Version()
    {
        var assembly = typeof(ArbiterController).Assembly;
        return Ok(new
        {
            version = assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion
                      ?? assembly.GetName().Version?.ToString()
                      ?? "unknown",
            description = "Averia RCCService Arbiter",
        });
    }

    [HttpGet("get-all-game-servers")]
    public ActionResult<ArbiterStatisticsResponse> GetAllGameServers()
    {
        return Ok(_pool.GetStatistics());
    }

    [HttpPost("start-game-server")]
    public async Task<ActionResult<StartGameServerResponse>> StartGameServer(
        [FromBody] StartGameServerRequest request,
        CancellationToken cancellationToken)
    {
        if (request.JobId == Guid.Empty)
        {
            return BadRequest(new { errors = new[] { new { code = 0, message = "jobId is required" } } });
        }

        try
        {
            return Ok(await _pool.StartGameServerAsync(request, cancellationToken));
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(new { errors = new[] { new { code = 0, message = ex.Message } } });
        }
    }

    [HttpPost("kill-game-server")]
    public async Task<ActionResult<ArbiterActionResponse>> KillGameServer(
        [FromBody] KillGameServerRequest request,
        CancellationToken cancellationToken)
    {
        return Ok(new ArbiterActionResponse
        {
            Success = await _pool.StopGameServerAsync(request.JobId, cancellationToken),
        });
    }

    [HttpPost("evict-player")]
    public async Task<ActionResult<ArbiterActionResponse>> EvictPlayer(
        [FromBody] EvictPlayerRequest request,
        CancellationToken cancellationToken)
    {
        return Ok(new ArbiterActionResponse
        {
            Success = await _pool.EvictPlayerAsync(request.GameId, request.UserId, request.MessageVersionId, cancellationToken),
        });
    }

    [HttpPost("set-filtering-enabled")]
    public async Task<ActionResult<ArbiterActionResponse>> SetFilteringEnabled(
        [FromBody] SetFilteringEnabledRequest request,
        CancellationToken cancellationToken)
    {
        return Ok(new ArbiterActionResponse
        {
            Success = await _pool.SetFilteringEnabledAsync(request.JobId, request.IsEnabled, cancellationToken),
        });
    }

    [HttpPost("clean-up")]
    public async Task<IActionResult> CleanUp(CancellationToken cancellationToken)
    {
        var removed = await _pool.CleanUpAsync(cancellationToken);
        return Ok(new
        {
            removed,
        });
    }
}
