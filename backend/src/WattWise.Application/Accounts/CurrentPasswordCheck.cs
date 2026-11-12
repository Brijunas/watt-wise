using NodaTime;

namespace WattWise.Application.Accounts;

/// <summary>The result of checking the password an account gives to change it. <see cref="LockoutEnd"/> is set only when it is locked out.</summary>
public sealed record CurrentPasswordCheck(CurrentPasswordCheckOutcome Outcome, Instant? LockoutEnd = null)
{
    public static CurrentPasswordCheck Verified() => new(CurrentPasswordCheckOutcome.Verified);

    public static CurrentPasswordCheck WrongPassword() => new(CurrentPasswordCheckOutcome.WrongPassword);

    public static CurrentPasswordCheck LockedOut(Instant lockoutEnd) => new(CurrentPasswordCheckOutcome.LockedOut, lockoutEnd);

    public static CurrentPasswordCheck NotFound() => new(CurrentPasswordCheckOutcome.NotFound);
}
