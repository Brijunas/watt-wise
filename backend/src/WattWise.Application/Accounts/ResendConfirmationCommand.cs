using Mediator;

using WattWise.Application.Results;

namespace WattWise.Application.Accounts;

/// <summary>Asks for a new confirmation link. Never looks the address up; the worker does.</summary>
public sealed record ResendConfirmationCommand(string? Email) : ICommand<Result<Unit>>
{
    // A record would print the address.
    public override string ToString() => nameof(ResendConfirmationCommand);
}
