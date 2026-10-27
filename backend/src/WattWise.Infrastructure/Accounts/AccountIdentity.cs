using Microsoft.AspNetCore.Identity;

namespace WattWise.Infrastructure.Accounts;

/// <summary>
/// The email and password way of proving who an account's user is. The email is a credential that
/// can change; the account it belongs to never does.
/// </summary>
public sealed class AccountIdentity : IdentityUser<Guid>
{
    private string? _email;
    private string? _userName;

    /// <summary>The account this <see cref="AccountIdentity"/> signs in to; unique, so an account has one in MVP.</summary>
    public Guid AccountId { get; init; }

    /// <summary>Trimmed, NFKC-normalized and lowercase, however it is assigned.</summary>
    public override string? Email
    {
        get => _email;
        set => _email = value is null ? null : TextNormalization.Email(value);
    }

    /// <summary>The same as the email.</summary>
    public override string? UserName
    {
        get => _userName;
        set => _userName = value is null ? null : TextNormalization.Email(value);
    }

    public static AccountIdentity Create(Guid accountId, string email)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(email);

        return new AccountIdentity
        {
            Id = Guid.CreateVersion7(),
            AccountId = accountId,
            Email = email,
            UserName = email,
        };
    }
}
