using WattWise.Application.Accounts;

namespace WattWise.Infrastructure.Accounts;

internal sealed class IdentityEmailAddressNormalizer : IEmailAddressNormalizer
{
    public string Normalize(string email)
    {
        return TextNormalization.Email(email);
    }
}
