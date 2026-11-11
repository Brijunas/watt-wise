namespace WattWise.Application.Accounts;

/// <summary>Checks an email confirmation link and confirms the address.</summary>
public interface IEmailConfirmation
{
    /// <summary>True when the link was valid and the address is now confirmed; false for every kind of bad link, with no way to tell them apart.</summary>
    Task<bool> ConfirmAsync(Guid accountIdentityId, string token, CancellationToken cancellationToken);
}
