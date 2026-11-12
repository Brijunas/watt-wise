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
    public const string SessionNotFoundCode = "Accounts.SessionNotFound";

    public const string InvalidCurrentPasswordCode = "Accounts.InvalidCurrentPassword";
    public const string InvalidEmailLinkCode = "Accounts.InvalidEmailLink";
    public const string PasswordChangeConflictCode = "Accounts.PasswordChangeConflict";

    /// <summary>A validation error and not a 401: the caller is signed in, and a 401 would make the client refresh.</summary>
    public static Error InvalidCurrentPassword()
    {
        const string message = "The current password is wrong.";
        return Error.Validation(
            InvalidCurrentPasswordCode,
            message,
            new Dictionary<string, string[]> { ["CurrentPassword"] = [message] });
    }

    public static Error InvalidEmailLink()
    {
        return Error.BadRequest(InvalidEmailLinkCode, "The link is invalid or has expired.");
    }

    public static Error PasswordChangeConflict()
    {
        return Error.Conflict(PasswordChangeConflictCode, "The account was changed by another request; try again.");
    }

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

    /// <summary>The same for an unknown id and another account's, so the answer reveals nothing about other accounts.</summary>
    public static Error SessionNotFound()
    {
        return Error.NotFound(SessionNotFoundCode, "The session does not exist.");
    }

    public static Error AccountNotFound()
    {
        return Error.NotFound(AccountNotFoundCode, "The account does not exist.");
    }
}
