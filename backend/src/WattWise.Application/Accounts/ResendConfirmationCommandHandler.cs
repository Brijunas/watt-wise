using Mediator;

using WattWise.Application.Email;
using WattWise.Application.Results;

namespace WattWise.Application.Accounts;

public sealed class ResendConfirmationCommandHandler(
    IEmailAddressNormalizer normalizer,
    IEmailRequests emailRequests,
    IUnitOfWork unitOfWork) : ICommandHandler<ResendConfirmationCommand, Result<Unit>>
{
    public async ValueTask<Result<Unit>> Handle(ResendConfirmationCommand command, CancellationToken cancellationToken)
    {
        emailRequests.Add(EmailRequestKind.EmailConfirmation, normalizer.Normalize(command.Email!));
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return Unit.Value;
    }
}
