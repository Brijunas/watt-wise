using Mediator;

using WattWise.Application.Results;

namespace WattWise.Application.Accounts;

public sealed class ConfirmEmailCommandHandler(IEmailConfirmation confirmation) : ICommandHandler<ConfirmEmailCommand, Result<Unit>>
{
    public async ValueTask<Result<Unit>> Handle(ConfirmEmailCommand command, CancellationToken cancellationToken)
    {
        bool confirmed = await confirmation.ConfirmAsync(Guid.Parse(command.Id!), command.Token!, cancellationToken);
        return confirmed
            ? Unit.Value
            : AccountErrors.InvalidEmailLink();
    }
}
