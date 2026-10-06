using WattWise.Application.Results;

namespace WattWise.Api.ErrorHandling;

/// <summary>
/// The single <see cref="ErrorType"/> to HTTP status table. The switch has no discard arm on
/// purpose: CS8509 is an error, so a new category does not compile until it is mapped here.
/// </summary>
public static class ErrorTypeMapping
{
    public static int ToStatusCode(ErrorType type)
    {
        return type switch
        {
            ErrorType.Validation => StatusCodes.Status400BadRequest,
            ErrorType.NotFound => StatusCodes.Status404NotFound,
            ErrorType.Unauthorized => StatusCodes.Status401Unauthorized,
        };
    }
}
