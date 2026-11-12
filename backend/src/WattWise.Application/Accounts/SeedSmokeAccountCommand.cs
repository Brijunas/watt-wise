using Mediator;

using WattWise.Application.Results;

namespace WattWise.Application.Accounts;

/// <summary>Brings one smoke-test account (an admin or a plain user) to a known state; Development only, enforced by the caller.</summary>
/// <param name="Email">As configured.</param>
/// <param name="Password">As configured.</param>
/// <param name="Admin">True when the account must hold the <c>Admin</c> role, false when it must not.</param>
public sealed record SeedSmokeAccountCommand(string? Email, string? Password, bool Admin) : ICommand<Result<SeedOutcome>>
{
    // A record would print the address and the password.
    public override string ToString() => nameof(SeedSmokeAccountCommand);
}
