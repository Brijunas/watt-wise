namespace WattWise.Domain.Exceptions;

/// <summary>A refresh token past its expiry was presented.</summary>
public sealed class RefreshTokenExpiredException : DomainException
{
    public const string ErrorCode = "Accounts.RefreshTokenExpired";

    public RefreshTokenExpiredException()
        : base(ErrorCode, "The refresh token has expired.")
    {
    }
}
