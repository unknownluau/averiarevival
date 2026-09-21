using Averia.RccServiceArbiter.Rendering;
using Microsoft.AspNetCore.Mvc;
using Roblox.Rendering;
using Roblox.Web.Infrastructure.Metadata;

namespace Averia.RccServiceArbiter.Controllers;

[ApiController]
[InternalServiceOnly]
[Route("")]
public sealed class RenderController(IRenderService renderer) : ControllerBase
{
    [HttpPost("render")]
    public async Task<ActionResult<RenderResult>> Render([FromBody] RenderRequest request, CancellationToken cancellationToken)
    {
        try
        {
            var output = await renderer.RenderAsync(request, cancellationToken);
            return Ok(new RenderResult
            {
                JobId = output.JobId,
                ContentType = output.ContentType,
                Data = Convert.ToBase64String(output.Data),
                DependencyUrls = output.DependencyUrls,
            });
        }
        catch (RenderValidationException ex) { return Error(StatusCodes.Status400BadRequest, ex.Message); }
        catch (RenderCapacityException ex) { return Error(StatusCodes.Status429TooManyRequests, ex.Message); }
        catch (TimeoutException ex) { return Error(StatusCodes.Status504GatewayTimeout, ex.Message); }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { throw; }
        catch (Exception ex) { return Error(StatusCodes.Status502BadGateway, ex.Message); }
    }

    [HttpPost("render/v2")]
    public async Task<IActionResult> RenderV2([FromBody] RenderRequest request, CancellationToken cancellationToken)
    {
        try
        {
            var output = await renderer.RenderAsync(request, cancellationToken);
            Response.Headers["X-Render-Job-Id"] = output.JobId.ToString("D");
            Response.Headers["X-Render-Worker-State"] = output.WorkerState;
            if (output.Timings.Count > 0)
            {
                Response.Headers["Server-Timing"] = string.Join(", ", output.Timings.Select(pair =>
                    $"{pair.Key};dur={pair.Value.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture)}"));
            }
            return File(output.Data, output.ContentType);
        }
        catch (RenderValidationException ex) { return Error(StatusCodes.Status400BadRequest, ex.Message); }
        catch (RenderCapacityException ex) { return Error(StatusCodes.Status429TooManyRequests, ex.Message); }
        catch (TimeoutException ex) { return Error(StatusCodes.Status504GatewayTimeout, ex.Message); }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { throw; }
        catch (Exception ex) { return Error(StatusCodes.Status502BadGateway, ex.Message); }
    }

    [HttpGet("render/statistics")]
    public ActionResult<RenderStatistics> Statistics() => Ok(renderer.GetStatistics());

    private ObjectResult Error(int status, string message) => StatusCode(status,
        new RenderErrorResponse { Errors = [new RenderError { Code = 0, Message = message }] });
}
