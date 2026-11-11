using Mediator;

using WattWise.Application.Results;
namespace WattWise.Application.Accounts;

public sealed class SignOutCommandHandler(IRefreshTokenFactory refreshTokens, ISessionRepository sessions, IUnitOfWork unitOfWork)
    : ICommandHandler<SignOutCommand, Result<Unit>>
{
    public async ValueTask<Result<Unit>> Handle(SignOutCommand command, CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(command.RefreshToken))
        {
            return Unit.Value;
        }

        // Used or not: a token that was exchanged still names its session.
        await SessionEnding.EndAsync(sessions, unitOfWork, refreshTokens.Hash(command.RefreshToken), cancellationToken);
        return Unit.Value;
    }
}
