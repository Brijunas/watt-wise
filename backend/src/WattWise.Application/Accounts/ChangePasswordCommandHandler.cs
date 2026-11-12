using Mediator;

using NodaTime;

using WattWise.Application.Results;

namespace WattWise.Application.Accounts;

public sealed class ChangePasswordCommandHandler(
    IPasswordChange passwordChange,
    IAccountSessions accountSessions,
    ITransactionRunner transactions,
    IAccountsQueries queries,
    IRefreshTokenFactory refreshTokens,
    IDeviceLabels deviceLabels,
    ISessionRepository sessions,
    IAccessTokenIssuer accessTokens,
    IUnitOfWork unitOfWork,
    IClock clock) : ICommandHandler<ChangePasswordCommand, Result<SignedIn>>
{
    public async ValueTask<Result<SignedIn>> Handle(ChangePasswordCommand command, CancellationToken cancellationToken)
    {
        // Outside the transaction: a wrong password's count must stay when the change fails.
        CurrentPasswordCheck check = await passwordChange.CheckCurrentAsync(command.AccountId, command.CurrentPassword!, cancellationToken);
        switch (check)
        {
            case CurrentPasswordCheck.WrongPassword:
                return AccountErrors.InvalidCurrentPassword();
            case CurrentPasswordCheck.LockedOut:
                return AccountErrors.LockedOut();
            case CurrentPasswordCheck.NotFound:
                return AccountErrors.AccountNotFound();
        }

        return await transactions.ExecuteAsync<SignedIn>(
            async token =>
            {
                PasswordChangeOutcome outcome = await passwordChange.ChangeAsync(
                    command.AccountId, command.CurrentPassword!, command.NewPassword!, token);
                if (!outcome.IsChanged)
                {
                    return outcome.Kind switch
                    {
                        PasswordChangeKind.WrongPassword => AccountErrors.InvalidCurrentPassword(),
                        PasswordChangeKind.NotFound => AccountErrors.AccountNotFound(),
                        _ => AccountErrors.PasswordChangeConflict(),
                    };
                }

                _ = await accountSessions.EndAllAsync(command.AccountId, token);

                IReadOnlyList<string> permissions = await queries.GetPermissionsAsync(command.AccountId, token);
                Instant now = clock.GetCurrentInstant();
                StartedSession started = SessionStarting.Start(
                    sessions, refreshTokens, deviceLabels, command.AccountId, command.UserAgent, outcome.SecurityStamp, now);
                try
                {
                    await unitOfWork.SaveChangesAsync(token);
                }
                catch (ConcurrencyConflictException)
                {
                    // Lost a race on the session rows; the failed result rolls the whole change back.
                    return AccountErrors.PasswordChangeConflict();
                }

                IssuedAccessToken accessToken = accessTokens.Issue(command.AccountId, started.Session.Id, permissions, now);
                return new SignedIn(accessToken.Token, accessToken.ExpiresAt, started.RefreshToken.Token, started.RefreshExpiresAt);
            },
            cancellationToken);
    }
}
