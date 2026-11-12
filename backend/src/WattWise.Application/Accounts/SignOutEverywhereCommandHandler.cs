using Mediator;

using WattWise.Application.Results;

namespace WattWise.Application.Accounts;

public sealed class SignOutEverywhereCommandHandler(IAccountSessions sessions) : ICommandHandler<SignOutEverywhereCommand, Result<Unit>>
{
    public async ValueTask<Result<Unit>> Handle(SignOutEverywhereCommand command, CancellationToken cancellationToken)
    {
        _ = await sessions.EndAllAsync(command.AccountId, cancellationToken);
        return Unit.Value;
    }
}
