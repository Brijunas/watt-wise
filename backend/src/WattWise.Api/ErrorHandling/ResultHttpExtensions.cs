using WattWise.Application.Results;

namespace WattWise.Api.ErrorHandling;

public static class ResultHttpExtensions
{
    /// <summary>
    /// Maps a result to HTTP: 200 with the value, or a ProblemDetails response whose title is the
    /// status default and whose detail is the error's description. The problem results are written through <see cref="IProblemDetailsService"/>, so the customization
    /// (<see cref="ProblemDetailsCustomization"/>) and <c>traceId</c> apply to them.
    /// </summary>
    public static IResult ToHttpResult<T>(this Result<T> result)
    {
        if (result.IsSuccess)
        {
            return TypedResults.Ok(result.Value);
        }

        Error error = result.Error;
        int status = ErrorTypeMapping.ToStatusCode(error.Type);
        Dictionary<string, object?> extensions = new() { ["code"] = error.Code };

        if (error.Type == ErrorType.Validation && error.FieldErrors is not null)
        {
            return TypedResults.ValidationProblem(error.FieldErrors.ToDictionary(), extensions: extensions);
        }

        return TypedResults.Problem(
            statusCode: status,
            detail: error.Description,
            extensions: extensions);
    }
}
