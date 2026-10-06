using WattWise.Domain.Exceptions;

namespace WattWise.Application.Results;

/// <summary>An expected failure of a use case.</summary>
/// <param name="Code">Machine-readable code in the form <c>Area.Reason</c>.</param>
/// <param name="Description">Human-readable explanation.</param>
/// <param name="Type">The failure category.</param>
/// <param name="FieldErrors">Messages per property name; set for validation errors only.</param>
public sealed record Error(
    string Code,
    string Description,
    ErrorType Type,
    IReadOnlyDictionary<string, string[]>? FieldErrors = null)
{
    public static Error Validation(IReadOnlyDictionary<string, string[]> fieldErrors)
    {
        return new Error("Validation.Failed", "One or more validation errors occurred.", ErrorType.Validation, fieldErrors);
    }

    public static Error NotFound(string code, string description)
    {
        return new Error(code, description, ErrorType.NotFound);
    }

    public static Error Unauthorized(string code, string description)
    {
        return new Error(code, description, ErrorType.Unauthorized);
    }

    /// <summary>Converts a domain exception a handler expected into an error of the given category.</summary>
    public static Error From(DomainException exception, ErrorType type)
    {
        return new Error(exception.Code, exception.Message, type);
    }
}
