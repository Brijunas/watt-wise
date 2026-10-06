namespace WattWise.Application.Results;

/// <summary>The outcome of a use case: a value, or an <see cref="Results.Error"/> for an expected failure.</summary>
/// <typeparam name="T">The value type of a success.</typeparam>
public sealed class Result<T> : IFallibleResult<Result<T>>
{
    private readonly T? _value;
    private readonly Error? _error;

    private Result(T value)
    {
        _value = value;
    }

    private Result(Error error)
    {
        _error = error;
    }

    public bool IsSuccess => _error is null;

    public bool IsFailure => _error is not null;

    /// <summary>The value. Throws <see cref="InvalidOperationException"/> when the result is a failure.</summary>
    public T Value => IsSuccess
        ? _value!
        : throw new InvalidOperationException("A failed result has no value.");

    /// <summary>The failure. Throws <see cref="InvalidOperationException"/> when the result is a success.</summary>
    public Error Error => _error
        ?? throw new InvalidOperationException("A successful result has no error.");

    public static Result<T> Success(T value)
    {
        return new Result<T>(value);
    }

    public static Result<T> Failure(Error error)
    {
        return new Result<T>(error);
    }

    public static implicit operator Result<T>(T value)
    {
        return Success(value);
    }

    public static implicit operator Result<T>(Error error)
    {
        return Failure(error);
    }

    public TOut Match<TOut>(Func<T, TOut> onSuccess, Func<Error, TOut> onFailure)
    {
        return IsSuccess ? onSuccess(_value!) : onFailure(_error!);
    }
}
