using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;

namespace WattWise.Infrastructure.Accounts;

/// <summary>
/// NFKC-normalizes a password before ASP.NET Core Identity's default hasher (PBKDF2-HMAC-SHA512, 100,000
/// iterations) sees it, so the same password typed on another keyboard still matches.
/// </summary>
internal sealed class NormalizingPasswordHasher(IOptions<PasswordHasherOptions> options) : IPasswordHasher<AccountIdentity>
{
    private readonly PasswordHasher<AccountIdentity> _inner = new(options);

    public string HashPassword(AccountIdentity user, string password)
    {
        return _inner.HashPassword(user, TextNormalization.Nfkc(password));
    }

    public PasswordVerificationResult VerifyHashedPassword(AccountIdentity user, string hashedPassword, string providedPassword)
    {
        return _inner.VerifyHashedPassword(user, hashedPassword, TextNormalization.Nfkc(providedPassword));
    }
}
