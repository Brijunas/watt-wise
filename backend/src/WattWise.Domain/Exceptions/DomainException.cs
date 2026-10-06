namespace WattWise.Domain.Exceptions;

/// <summary>
/// Base type for exceptions the domain throws when an invariant is broken. Handlers catch the
/// ones they expect and convert them to errors; any other one is a bug and surfaces as a 500.
/// </summary>
public abstract class DomainException : Exception
{
    protected DomainException(string code, string message)
        : base(message)
    {
        Code = code;
    }

    protected DomainException(string code, string message, Exception innerException)
        : base(message, innerException)
    {
        Code = code;
    }

    /// <summary>Machine-readable error code in the form <c>Area.Reason</c>.</summary>
    public string Code { get; }
}
