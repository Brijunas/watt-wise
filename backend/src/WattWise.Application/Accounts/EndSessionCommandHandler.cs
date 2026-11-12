using Mediator;

using WattWise.Application.Results;

namespace WattWise.Application.Accounts;

public sealed class EndSessionCommandHandler(IAccountSessions sessions) : ICommandHandler<EndSessionCommand, Result<Unit>>
{
    public async ValueTask<Result<Unit>> Handle(EndSessionCommand command, CancellationToken cancellationToken)
    {
        int ended = await sessions.EndAsync(command.AccountId, command.SessionId, cancellationToken);
        if (ended == 0)
        {
            return AccountErrors.SessionNotFound();
        }

        return Unit.Value;
    }
}
