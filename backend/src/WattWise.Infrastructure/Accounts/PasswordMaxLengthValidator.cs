using Microsoft.AspNetCore.Identity;

namespace WattWise.Infrastructure.Accounts;

/// <summary>Rejects passwords longer than <see cref="MaxLength"/> characters once NFKC-normalized (the form that is hashed), so hashing one can't be used to burn CPU.</summary>
internal sealed class PasswordMaxLengthValidator : IPasswordValidator<AccountIdentity>
{
    public const int MaxLength = 256;

    public Task<IdentityResult> ValidateAsync(UserManager<AccountIdentity> manager, AccountIdentity user, string? password)
    {
        ArgumentNullException.ThrowIfNull(password);

        IdentityResult result = TextNormalization.Nfkc(password).Length > MaxLength
            ? IdentityResult.Failed(new IdentityError
            {
                Code = "PasswordTooLong",
                Description = $"Passwords must be at most {MaxLength} characters.",
            })
            : IdentityResult.Success;
        return Task.FromResult(result);
    }
}
