using System.Net;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Roblox.Exceptions;
using Roblox.Libraries.Exceptions;
using Roblox.Services.Exceptions;
using Roblox.Website.WebsiteModels;

namespace Roblox.Website.ExceptionHandlers;

public sealed class GlobalExceptionHandler : IExceptionHandler
{
    private readonly ILogger<GlobalExceptionHandler> _logger;

    public GlobalExceptionHandler(ILogger<GlobalExceptionHandler> logger)
    {
        _logger = logger;
    }

    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        var (statusCode, errorList) = MapException(exception);

#if DEBUG
        var firstError = errorList.First();
        firstError.message = $"{firstError.message}\n{exception.Message}\n{exception.StackTrace}";
#endif

        httpContext.Response.StatusCode = (int)statusCode;

        await httpContext.Response.WriteAsJsonAsync(
            new ErrorResponse { errors = errorList },
            cancellationToken
        );

        // Return true to signal the exception was handled
        return true;
    }

    private (HttpStatusCode statusCode, List<ErrorResponseEntry> errors) MapException(Exception exception)
    {
        var errorList = new List<ErrorResponseEntry>();
        var code = HttpStatusCode.InternalServerError;

        switch (exception)
        {
            case HttpBaseException httpException:
                code = httpException.statusCode;
                foreach (var err in httpException.errors)
                    errorList.Add(new() { message = err.errorMessage, code = err.errorCode });
                break;

            case LogicException logicException:
                code = logicException.failType switch
                {
                    FailType.Unknown    => HttpStatusCode.InternalServerError,
                    FailType.BadRequest => HttpStatusCode.BadRequest,
                    FailType.FloodCheck => HttpStatusCode.TooManyRequests,
                    _ => throw new Exception($"Unexpected failType {logicException.failType}")
                };
                errorList.Add(new() { code = logicException.errorCode, message = logicException.errorMessage });
                break;

            case RecordNotFoundException:
                code = HttpStatusCode.BadRequest;
                errorList.Add(new() { code = 0, message = "NotFound" });
                break;

            case RobloxException ex:
                code = (HttpStatusCode)ex.statusCode;
                errorList.Add(new() { code = ex.errorCode, message = ex.errorMessage });
                break;

            case Roblox.Services.CooldownException:
                code = HttpStatusCode.TooManyRequests;
                errorList.Add(new() { code = 0, message = "Too many requests. Try again in a few minutes." });
                break;

            default:
                _logger.LogError(exception, "Unhandled exception caught by GlobalExceptionHandler");
                break;
        }

        if (errorList.Count == 0)
            errorList.Add(new() { message = "InternalServerError", code = 0 });

        return (code, errorList);
    }
}