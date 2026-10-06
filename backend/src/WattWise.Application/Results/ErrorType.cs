namespace WattWise.Application.Results;

/// <summary>The category of an expected failure. Carries no HTTP detail; the Api maps it to a status.</summary>
public enum ErrorType
{
    Validation,
    NotFound,
    Unauthorized,
}
