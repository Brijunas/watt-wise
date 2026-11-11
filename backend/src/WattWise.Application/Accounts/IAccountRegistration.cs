using WattWise.Domain.Localization;

namespace WattWise.Application.Accounts;

/// <summary>Creates accounts with their sign-in record. Nothing is saved until the unit of work or <see cref="CreateIdentityAsync"/> saves.</summary>
public interface IAccountRegistration
{
    /// <summary>The id of the account that owns the address, or null when there is none.</summary>
    Task<Guid?> FindAccountIdAsync(string normalizedEmail, CancellationToken cancellationToken);

    /// <summary>Stages a new account and returns its id.</summary>
    Guid StageAccount(Language preferredLanguage);

    /// <summary>
    /// Creates the unconfirmed sign-in record for a staged account and saves everything staged, the email request included.
    /// Returns false when the address was taken in the meantime.
    /// </summary>
    Task<bool> CreateIdentityAsync(Guid accountId, string normalizedEmail, string password, CancellationToken cancellationToken);

    /// <summary>Hashes the password and drops the hash, so a taken address takes as long as a new one.</summary>
    void HashAndDiscard(string password);
}
