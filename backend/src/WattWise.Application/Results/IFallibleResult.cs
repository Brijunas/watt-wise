namespace WattWise.Application.Results;

/// <summary>A result that can fail with an <see cref="Results.Error"/>; lets generic behaviors short-circuit any result type.</summary>
/// <typeparam name="TSelf">The implementing result type.</typeparam>
public interface IFallibleResult<TSelf>
    where TSelf : IFallibleResult<TSelf>
{
    bool IsFailure { get; }

    /// <summary>The failure. Throws <see cref="InvalidOperationException"/> when the result is a success.</summary>
    Error Error { get; }

    static abstract TSelf Failure(Error error);
}
