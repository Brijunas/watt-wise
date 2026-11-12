namespace WattWise.Application.Accounts;

public enum CurrentPasswordCheckOutcome
{
    Verified,
    WrongPassword,
    LockedOut,
    NotFound,
}
