namespace WattWise.Domain.Exceptions;

/// <summary>A refresh token that was already exchanged was presented again.</summary>
public sealed class RefreshTokenReusedException : DomainException
{
    public const string ErrorCode = "Accounts.RefreshTokenReused";

    public RefreshTokenReusedException()
        : base(ErrorCode, "The refresh token was already used.")
    {
    }
}
