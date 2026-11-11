using Mediator;

using WattWise.Application.Results;

namespace WattWise.Application.Accounts;

/// <summary>Ends the session the refresh token belongs to. Always succeeds.</summary>
/// <param name="RefreshToken">The token from the cookie; <c>null</c> when there was none.</param>
public sealed record SignOutCommand(string? RefreshToken) : ICommand<Result<Unit>>
{
    // A record would print the token.
    public override string ToString() => nameof(SignOutCommand);
}
