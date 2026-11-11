using Mediator;

using WattWise.Application.Results;

namespace WattWise.Application.Accounts;

public sealed class ConfirmEmailCommandHandler(IEmailConfirmation confirmation) : ICommandHandler<ConfirmEmailCommand, Result<Unit>>
{
    public const string InvalidEmailLinkCode = "Accounts.InvalidEmailLink";

    public async ValueTask<Result<Unit>> Handle(ConfirmEmailCommand command, CancellationToken cancellationToken)
    {
        bool confirmed = await confirmation.ConfirmAsync(Guid.Parse(command.Id!), command.Token!, cancellationToken);
        return confirmed
            ? Unit.Value
            : Error.BadRequest(InvalidEmailLinkCode, "The link is invalid or has expired.");
    }
}
