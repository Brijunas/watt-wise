namespace WattWise.Application.Accounts;

/// <summary>The password rules from security.md, applied to the form that is hashed.</summary>
public interface IPasswordPolicy
{
    /// <summary>Returns a message per broken rule; empty when the password is acceptable.</summary>
    Task<IReadOnlyList<string>> CheckAsync(string password, CancellationToken cancellationToken);
}
