namespace WattWise.Application.Accounts;

/// <summary>The result of checking an email and password. <see cref="AccountId"/> is set only when it succeeded.</summary>
public sealed record PasswordCheck(PasswordCheckOutcome Outcome, Guid? AccountId = null)
{
    public static PasswordCheck Succeeded(Guid accountId) => new(PasswordCheckOutcome.Succeeded, accountId);

    public static PasswordCheck InvalidCredentials() => new(PasswordCheckOutcome.InvalidCredentials);

    public static PasswordCheck EmailNotConfirmed() => new(PasswordCheckOutcome.EmailNotConfirmed);

    public static PasswordCheck LockedOut() => new(PasswordCheckOutcome.LockedOut);
}
