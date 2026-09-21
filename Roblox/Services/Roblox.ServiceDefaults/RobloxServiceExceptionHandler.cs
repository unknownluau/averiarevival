using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Http;
using Roblox.Services.Exceptions;

namespace Roblox.ServiceDefaults;

public sealed class RobloxServiceExceptionHandler : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        var error = MapException(exception);
        if (error == null)
        {
            return false;
        }

        httpContext.Response.StatusCode = error.statusCode;
        await httpContext.Response.WriteAsJsonAsync(
            new RobloxServiceErrorResponse
            {
                errors = new[]
                {
                    new RobloxServiceErrorEntry
                    {
                        code = error.code,
                        message = error.message,
                    },
                },
            },
            cancellationToken);

        return true;
    }

    private static RobloxServiceError? MapException(Exception exception)
    {
        return exception switch
        {
            RobloxException ex => new RobloxServiceError(ex.statusCode, ex.errorCode, ex.errorMessage),
            RecordNotFoundException => new RobloxServiceError(StatusCodes.Status400BadRequest, 0, "NotFound"),
            _ => null,
        };
    }

    private sealed record RobloxServiceError(int statusCode, int code, string message);

    private sealed class RobloxServiceErrorResponse
    {
        public IReadOnlyList<RobloxServiceErrorEntry> errors { get; set; } = Array.Empty<RobloxServiceErrorEntry>();
    }

    private sealed class RobloxServiceErrorEntry
    {
        public int code { get; set; }
        public string message { get; set; } = string.Empty;
    }
}
