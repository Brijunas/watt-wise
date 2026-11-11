using Mediator;

using WattWise.Application.Results;

namespace WattWise.Application.Accounts;

/// <summary>Signs up with an email and password. Answers the same for a new and a taken address.</summary>
/// <param name="Email">As typed.</param>
/// <param name="Password">As typed.</param>
/// <param name="Language">The account's preferred language: <c>lt</c> or <c>en</c>.</param>
public sealed record RegisterAccountCommand(string? Email, string? Password, string? Language) : ICommand<Result<Unit>>
{
    // A record would print the password.
    public override string ToString() => nameof(RegisterAccountCommand);
}
