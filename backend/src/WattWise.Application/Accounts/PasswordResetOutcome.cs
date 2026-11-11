namespace WattWise.Application.Accounts;

/// <summary>The result of using a reset link. <see cref="AccountId"/> is set only when the password was reset.</summary>
public sealed record PasswordResetOutcome(Guid? AccountId = null)
{
    public bool IsReset => AccountId is not null;

    public static PasswordResetOutcome Reset(Guid accountId) => new(accountId);

    /// <summary>Every kind of bad link, with no way to tell them apart.</summary>
    public static PasswordResetOutcome InvalidLink() => new();
}
