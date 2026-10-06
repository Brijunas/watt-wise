using System.Text.Json;

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
        Dictionary<string, object?> extensions = new() { [ProblemDetailsKeys.Code] = error.Code };

        if (error.Type == ErrorType.Validation && error.FieldErrors is not null)
        {
            // TypedResults.ValidationProblem is fixed at 400 and cannot carry another status.
            return TypedResults.ValidationProblem(ToCamelCaseKeys(error.FieldErrors), extensions: extensions);
        }

        return TypedResults.Problem(
            statusCode: ErrorTypeMapping.ToStatusCode(error.Type),
            detail: error.Description,
            extensions: extensions);
    }

    /// <summary>
    /// Field keys are the validators' property names; the API's JSON uses camel case, so each
    /// dot-separated segment is converted. Indexers survive: <c>Items[0].Name</c> gives <c>items[0].name</c>.
    /// </summary>
    private static Dictionary<string, string[]> ToCamelCaseKeys(IReadOnlyDictionary<string, string[]> fieldErrors)
    {
        return fieldErrors.ToDictionary(
            pair => string.Join('.', pair.Key.Split('.').Select(segment => JsonNamingPolicy.CamelCase.ConvertName(segment))),
            pair => pair.Value);
    }
}
