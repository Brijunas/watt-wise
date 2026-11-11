using WattWise.Application.Accounts;

namespace WattWise.Application.Tests.Accounts;

/// <summary>Marks an address as normalized, so a test sees whether the handler used the normalized form.</summary>
internal sealed class PrefixingEmailNormalizer : IEmailAddressNormalizer
{
    public string Normalize(string email) => $"normalized:{email}";
}
