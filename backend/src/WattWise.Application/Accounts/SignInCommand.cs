using Mediator;

using WattWise.Application.Results;

namespace WattWise.Application.Accounts;

/// <summary>Signs in with an email and password and starts a session.</summary>
/// <param name="Email">As typed.</param>
/// <param name="Password">As typed.</param>
/// <param name="UserAgent">The request's User-Agent header, for the session's device label.</param>
/// <param name="RefreshToken">The refresh cookie the request carried, if any; its session ends when it belongs to the account that signed in.</param>
public sealed record SignInCommand(string? Email, string? Password, string? UserAgent, string? RefreshToken = null) : ICommand<Result<SignedIn>>
{
    // A record would print the password.
    public override string ToString() => nameof(SignInCommand);
}
