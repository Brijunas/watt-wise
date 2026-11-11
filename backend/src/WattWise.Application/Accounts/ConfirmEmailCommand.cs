using Mediator;

using WattWise.Application.Results;

namespace WattWise.Application.Accounts;

/// <summary>Uses an email confirmation link.</summary>
/// <param name="Id">The <c>AccountIdentityId</c> in the link, as text so a malformed id is a validation error.</param>
/// <param name="Token">The link's token.</param>
public sealed record ConfirmEmailCommand(string? Id, string? Token) : ICommand<Result<Unit>>
{
    // A record would print the token.
    public override string ToString() => nameof(ConfirmEmailCommand);
}
