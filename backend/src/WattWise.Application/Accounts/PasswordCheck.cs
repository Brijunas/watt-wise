using NodaTime;

namespace WattWise.Application.Accounts;

/// <summary>
/// The result of checking an email and password. <see cref="AccountId"/> and <see cref="SecurityStamp"/> (the one the
/// password was checked against) are set only when it succeeded, and <see cref="LockoutEnd"/> only when the account is locked out.
/// </summary>
public sealed record PasswordCheck(PasswordCheckOutcome Outcome, Guid? AccountId = null, string? SecurityStamp = null, Instant? LockoutEnd = null)
{
    public static PasswordCheck Succeeded(Guid accountId, string securityStamp) => new(PasswordCheckOutcome.Succeeded, accountId, securityStamp);

    public static PasswordCheck InvalidCredentials() => new(PasswordCheckOutcome.InvalidCredentials);

    public static PasswordCheck EmailNotConfirmed() => new(PasswordCheckOutcome.EmailNotConfirmed);

    public static PasswordCheck LockedOut(Instant lockoutEnd) => new(PasswordCheckOutcome.LockedOut, LockoutEnd: lockoutEnd);

    // A record would print the stamp.
    public override string ToString() => $"{nameof(PasswordCheck)} {Outcome}";
}
