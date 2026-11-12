using Mediator;

using WattWise.Application.Results;

namespace WattWise.Application.Accounts;

/// <summary>Sets a new password from a reset link and ends every session of the account.</summary>
/// <param name="Id">The <c>AccountIdentityId</c> in the link, as text so a malformed id is a validation error.</param>
/// <param name="Token">The link's token.</param>
/// <param name="NewPassword">As typed.</param>
public sealed record ResetPasswordCommand(string? Id, string? Token, string? NewPassword) : ICommand<Result<Unit>>
{
    // A record would print the token and the password.
    public override string ToString() => nameof(ResetPasswordCommand);
}
