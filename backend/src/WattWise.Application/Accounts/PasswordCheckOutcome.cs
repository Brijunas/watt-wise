namespace WattWise.Application.Accounts;

public enum PasswordCheckOutcome
{
    Succeeded,
    InvalidCredentials,
    EmailNotConfirmed,
    LockedOut,
}
