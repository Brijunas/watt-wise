using Mediator;

using WattWise.Application.Results;

namespace WattWise.Application.Accounts;

public sealed class ResetPasswordCommandHandler(
    IPasswordReset passwordReset,
    IAccountSessions accountSessions,
    ITransactionRunner transactions) : ICommandHandler<ResetPasswordCommand, Result<Unit>>
{
    public async ValueTask<Result<Unit>> Handle(ResetPasswordCommand command, CancellationToken cancellationToken)
    {
        // One transaction: a rejected link rolls back and leaves the sessions, and a failed session delete leaves the old password.
        return await transactions.ExecuteAsync<Unit>(
            async token =>
            {
                PasswordResetOutcome outcome = await passwordReset.ResetAsync(Guid.Parse(command.Id!), command.Token!, command.NewPassword!, token);
                if (!outcome.IsReset)
                {
                    return AccountErrors.InvalidEmailLink();
                }

                _ = await accountSessions.EndAllAsync(outcome.AccountId!.Value, token);
                return Unit.Value;
            },
            cancellationToken);
    }
}
