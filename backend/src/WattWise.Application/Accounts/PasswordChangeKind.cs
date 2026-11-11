namespace WattWise.Application.Accounts;

public enum PasswordChangeKind
{
    Changed,
    WrongPassword,
    NotFound,

    /// <summary>Another request changed the account's sign-in record at the same time.</summary>
    Conflict,
}
