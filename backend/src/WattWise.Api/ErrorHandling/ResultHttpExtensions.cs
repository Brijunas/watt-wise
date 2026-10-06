using Microsoft.AspNetCore.Http.HttpResults;

using WattWise.Application.Results;

namespace WattWise.Api.ErrorHandling;

public static class ResultHttpExtensions
{
    /// <summary>
    /// Maps a result to HTTP: 200 with the value, a 400 validation problem with the field errors, or
    /// a ProblemDetails response whose title is the status default and whose detail is the error's
    /// description. The problem results are written through <see cref="IProblemDetailsService"/>, so
    /// the customization (<see cref="ProblemDetailsCustomization"/>) and <c>traceId</c> apply to them.
    /// The typed return lets OpenAPI metadata flow from the endpoint's signature.
    /// </summary>
    public static Results<Ok<T>, ProblemHttpResult, ValidationProblem> ToHttpResult<T>(this Result<T> result)
    {
        if (result.IsSuccess)
        {
            return TypedResults.Ok(result.Value);
        }

        Error error = result.Error;
        Dictionary<string, object?> extensions = new() { ["code"] = error.Code };

        if (error.Type == ErrorType.Validation && error.FieldErrors is not null)
        {
            // TypedResults.ValidationProblem is fixed at 400 and cannot carry another status.
            return TypedResults.ValidationProblem(error.FieldErrors.ToDictionary(), extensions: extensions);
        }

        return TypedResults.Problem(
            statusCode: ErrorTypeMapping.ToStatusCode(error.Type),
            detail: error.Description,
            extensions: extensions);
    }
}
