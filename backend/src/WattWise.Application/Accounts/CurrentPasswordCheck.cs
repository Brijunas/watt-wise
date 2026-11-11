namespace WattWise.Application.Accounts;

/// <summary>The result of checking the password an account gives to change it.</summary>
public enum CurrentPasswordCheck
{
    Verified,
    WrongPassword,
    LockedOut,
    NotFound,
}
