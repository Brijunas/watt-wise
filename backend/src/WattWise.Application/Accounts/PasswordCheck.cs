namespace WattWise.Application.Accounts;

/// <summary>
/// The result of checking an email and password. <see cref="AccountId"/> and <see cref="SecurityStamp"/> (the one the
/// password was checked against) are set only when it succeeded.
/// </summary>
public sealed record PasswordCheck(PasswordCheckOutcome Outcome, Guid? AccountId = null, string? SecurityStamp = null)
{
    public static PasswordCheck Succeeded(Guid accountId, string securityStamp) => new(PasswordCheckOutcome.Succeeded, accountId, securityStamp);

    public static PasswordCheck InvalidCredentials() => new(PasswordCheckOutcome.InvalidCredentials);

    public static PasswordCheck EmailNotConfirmed() => new(PasswordCheckOutcome.EmailNotConfirmed);

    public static PasswordCheck LockedOut() => new(PasswordCheckOutcome.LockedOut);

    // A record would print the stamp.
    public override string ToString() => $"{nameof(PasswordCheck)} {Outcome}";
}
