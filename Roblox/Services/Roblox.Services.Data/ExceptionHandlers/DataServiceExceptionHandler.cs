using Microsoft.AspNetCore.Diagnostics;
using Roblox.Services.Exceptions;

namespace Roblox.Services.Data.ExceptionHandlers;

public sealed class DataServiceExceptionHandler : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        if (exception is not RobloxException robloxException)
        {
            return false;
        }

        httpContext.Response.StatusCode = robloxException.statusCode;
        await httpContext.Response.WriteAsJsonAsync(
            new
            {
                errors = new[]
                {
                    new
                    {
                        code = robloxException.errorCode,
                        message = robloxException.errorMessage,
                    },
                },
            },
            cancellationToken
        );

        return true;
    }
}
