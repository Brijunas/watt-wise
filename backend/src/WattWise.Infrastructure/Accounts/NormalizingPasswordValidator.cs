using Microsoft.AspNetCore.Identity;

namespace WattWise.Infrastructure.Accounts;

/// <summary>
/// ASP.NET Core Identity's <see cref="PasswordValidator{TUser}"/> (length, character classes) applied to the
/// NFKC form of the password, the same string <see cref="NormalizingPasswordHasher"/> hashes.
/// </summary>
internal sealed class NormalizingPasswordValidator(IdentityErrorDescriber describer) : IPasswordValidator<AccountIdentity>
{
    private readonly PasswordValidator<AccountIdentity> _inner = new(describer);

    public Task<IdentityResult> ValidateAsync(UserManager<AccountIdentity> manager, AccountIdentity user, string? password)
    {
        ArgumentNullException.ThrowIfNull(password);

        return _inner.ValidateAsync(manager, user, TextNormalization.Nfkc(password));
    }
}
