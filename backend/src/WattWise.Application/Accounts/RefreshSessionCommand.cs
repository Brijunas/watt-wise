using Mediator;

using WattWise.Application.Results;

namespace WattWise.Application.Accounts;

/// <summary>Exchanges a refresh token for a new access token and a new refresh token.</summary>
/// <param name="RefreshToken">The token from the cookie; <c>null</c> when there was none.</param>
public sealed record RefreshSessionCommand(string? RefreshToken) : ICommand<Result<SignedIn>>
{
    // A record would print the token.
    public override string ToString() => nameof(RefreshSessionCommand);
}
