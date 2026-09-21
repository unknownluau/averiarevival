namespace Roblox.Services.Exceptions;

public class RobloxException(
    int statusCode = RobloxException.InternalServerError,
    int errorCode = 0,
    string message = "")
    : System.Exception("Roblox Exception: " + statusCode + "\n" + errorCode + ": " + message)
{
    public const int NotFound = 404;
    public const int BadRequest = 400;
    public const int Unauthorized = 401;
    public const int Forbidden = 403;
    public const int TooManyRequests = 429;
    public const int InternalServerError = 500;
    
    public int statusCode { get; set; } = statusCode;
    public int errorCode { get; set; } = errorCode;
    public string errorMessage { get; set; } = message;
}