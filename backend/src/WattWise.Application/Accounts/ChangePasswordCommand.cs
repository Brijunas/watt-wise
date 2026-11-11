using Mediator;

using WattWise.Application.Results;

namespace WattWise.Application.Accounts;

/// <summary>Changes the signed-in account's password, ends all its sessions and starts a new one for the caller.</summary>
/// <param name="AccountId">The caller's account, from the access token.</param>
/// <param name="CurrentPassword">As typed.</param>
/// <param name="NewPassword">As typed.</param>
/// <param name="UserAgent">The request's User-Agent header, for the new session's device label.</param>
public sealed record ChangePasswordCommand(Guid AccountId, string? CurrentPassword, string? NewPassword, string? UserAgent) : ICommand<Result<SignedIn>>
{
    // A record would print the passwords.
    public override string ToString() => nameof(ChangePasswordCommand);
}
