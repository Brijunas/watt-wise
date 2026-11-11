namespace WattWise.Application.Accounts;

/// <summary>Turns an address into the form accounts are stored and looked up in (trimmed, NFKC, lowercase).</summary>
public interface IEmailAddressNormalizer
{
    string Normalize(string email);
}
