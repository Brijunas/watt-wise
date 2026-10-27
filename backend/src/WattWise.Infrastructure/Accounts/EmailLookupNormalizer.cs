using Microsoft.AspNetCore.Identity;

namespace WattWise.Infrastructure.Accounts;

/// <summary>Normalizes names and emails the same way: trimmed, NFKC-normalized, lowercase.</summary>
internal sealed class EmailLookupNormalizer : ILookupNormalizer
{
    public string? NormalizeName(string? name)
    {
        return name is null ? null : TextNormalization.Email(name);
    }

    public string? NormalizeEmail(string? email)
    {
        return email is null ? null : TextNormalization.Email(email);
    }
}
