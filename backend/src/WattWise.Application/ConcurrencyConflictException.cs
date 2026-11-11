namespace WattWise.Application;

/// <summary>A save lost a race: another request changed the same rows first. The unit of work has dropped its tracked changes.</summary>
public sealed class ConcurrencyConflictException : Exception
{
    public ConcurrencyConflictException()
        : base("The data was changed by another request.")
    {
    }

    public ConcurrencyConflictException(Exception innerException)
        : base("The data was changed by another request.", innerException)
    {
    }
}
