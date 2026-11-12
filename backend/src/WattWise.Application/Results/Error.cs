using NodaTime;

using WattWise.Domain.Exceptions;

namespace WattWise.Application.Results;

/// <summary>An expected failure of a use case.</summary>
/// <param name="Code">Machine-readable code in the form <c>Area.Reason</c>.</param>
/// <param name="Description">Human-readable explanation.</param>
/// <param name="Type">The failure category.</param>
/// <param name="FieldErrors">Messages per property name; set for validation errors only.</param>
/// <param name="RetryAfter">How long until trying again can succeed; set for a locked error that knows it.</param>
public sealed record Error(
    string Code,
    string Description,
    ErrorType Type,
    IReadOnlyDictionary<string, string[]>? FieldErrors = null,
    Duration? RetryAfter = null)
{
    public static Error Validation(IReadOnlyDictionary<string, string[]> fieldErrors)
    {
        return new Error("Validation.Failed", "One or more validation errors occurred.", ErrorType.Validation, fieldErrors);
    }

    /// <summary>A validation error with its own code, for a rule that needs data and so can't be a validator's.</summary>
    public static Error Validation(string code, string description, IReadOnlyDictionary<string, string[]> fieldErrors)
    {
        return new Error(code, description, ErrorType.Validation, fieldErrors);
    }

    public static Error NotFound(string code, string description)
    {
        return new Error(code, description, ErrorType.NotFound);
    }

    public static Error Unauthorized(string code, string description)
    {
        return new Error(code, description, ErrorType.Unauthorized);
    }

    /// <summary>The caller is known but not allowed to do this.</summary>
    public static Error Forbidden(string code, string description)
    {
        return new Error(code, description, ErrorType.Forbidden);
    }

    /// <summary>The resource is locked for now, e.g. an account locked out after failed sign-ins.</summary>
    public static Error Locked(string code, string description, Duration? retryAfter = null)
    {
        return new Error(code, description, ErrorType.Locked, RetryAfter: retryAfter);
    }

    /// <summary>The request lost a race: another request changed the same data first, and trying again may succeed.</summary>
    public static Error Conflict(string code, string description)
    {
        return new Error(code, description, ErrorType.Conflict);
    }

    /// <summary>A well-formed request that cannot be carried out and is not a field validation error.</summary>
    public static Error BadRequest(string code, string description)
    {
        return new Error(code, description, ErrorType.BadRequest);
    }

    /// <summary>Converts a domain exception a handler expected into an error of the given category.</summary>
    public static Error From(DomainException exception, ErrorType type)
    {
        return new Error(exception.Code, exception.Message, type);
    }
}
