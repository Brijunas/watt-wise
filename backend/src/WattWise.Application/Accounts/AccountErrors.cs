using WattWise.Application.Results;

namespace WattWise.Application.Accounts;

/// <summary>The errors of sign-in, refresh and the account's own data.</summary>
public static class AccountErrors
{
    public const string InvalidCredentialsCode = "Accounts.InvalidCredentials";
    public const string EmailNotConfirmedCode = "Accounts.EmailNotConfirmed";
    public const string LockedOutCode = "Accounts.LockedOut";
    public const string InvalidRefreshTokenCode = "Accounts.InvalidRefreshToken";
    public const string AccountNotFoundCode = "Accounts.AccountNotFound";

    public static Error InvalidCredentials()
    {
        return Error.Unauthorized(InvalidCredentialsCode, "The email or password is wrong.");
    }

    public static Error EmailNotConfirmed()
    {
        return Error.Forbidden(EmailNotConfirmedCode, "The email address is not confirmed.");
    }

    public static Error LockedOut()
    {
        return Error.Locked(LockedOutCode, "Too many failed sign-ins; try again later.");
    }

    public static Error InvalidRefreshToken()
    {
        return Error.Unauthorized(InvalidRefreshTokenCode, "The refresh token is missing, invalid or expired.");
    }

    public static Error AccountNotFound()
    {
        return Error.NotFound(AccountNotFoundCode, "The account does not exist.");
    }
}
