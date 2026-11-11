using Mediator;

using WattWise.Application.Email;
using WattWise.Application.Results;

namespace WattWise.Application.Accounts;

public sealed class ForgotPasswordCommandHandler(
    IEmailAddressNormalizer normalizer,
    IEmailRequests emailRequests,
    IUnitOfWork unitOfWork) : ICommandHandler<ForgotPasswordCommand, Result<Unit>>
{
    public async ValueTask<Result<Unit>> Handle(ForgotPasswordCommand command, CancellationToken cancellationToken)
    {
        emailRequests.Add(EmailRequestKind.PasswordReset, normalizer.Normalize(command.Email!));
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return Unit.Value;
    }
}
