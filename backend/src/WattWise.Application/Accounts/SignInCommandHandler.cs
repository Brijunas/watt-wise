using Mediator;

using NodaTime;

using WattWise.Application.Results;
using WattWise.Domain.Accounts;

namespace WattWise.Application.Accounts;

public sealed class SignInCommandHandler(
    IEmailAddressNormalizer normalizer,
    IPasswordSignIn passwordSignIn,
    IAccountsQueries queries,
    IRefreshTokenFactory refreshTokens,
    IDeviceLabels deviceLabels,
    ISessionRepository sessions,
    IAccessTokenIssuer accessTokens,
    IUnitOfWork unitOfWork,
    IClock clock) : ICommandHandler<SignInCommand, Result<SignedIn>>
{
    public async ValueTask<Result<SignedIn>> Handle(SignInCommand command, CancellationToken cancellationToken)
    {
        PasswordCheck check = await passwordSignIn.CheckAsync(normalizer.Normalize(command.Email!), command.Password!, cancellationToken);
        switch (check.Outcome)
        {
            case PasswordCheckOutcome.InvalidCredentials:
                return AccountErrors.InvalidCredentials();
            case PasswordCheckOutcome.EmailNotConfirmed:
                return AccountErrors.EmailNotConfirmed();
            case PasswordCheckOutcome.LockedOut:
                return AccountErrors.LockedOut();
        }

        Guid accountId = check.AccountId!.Value;
        IReadOnlyList<string> permissions = await queries.GetPermissionsAsync(accountId, cancellationToken);

        Instant now = clock.GetCurrentInstant();
        StartedSession started = SessionStarting.Start(
            sessions, refreshTokens, deviceLabels, accountId, command.UserAgent, check.SecurityStamp!, now);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        // Signing in again on a device ends the session it was in, but only when it was this account's: a cookie of
        // another account, or an unknown one, is none of this request's business.
        if (!string.IsNullOrEmpty(command.RefreshToken))
        {
            byte[] previousHash = refreshTokens.Hash(command.RefreshToken);
            Session? previous = await sessions.GetByRefreshTokenHashAsync(previousHash, cancellationToken);
            if (previous is not null && previous.AccountId == accountId)
            {
                await SessionEnding.EndAsync(sessions, unitOfWork, previousHash, cancellationToken);
            }
        }

        IssuedAccessToken accessToken = accessTokens.Issue(accountId, started.Session.Id, permissions, now);
        return new SignedIn(accessToken.Token, accessToken.ExpiresAt, started.RefreshToken.Token, started.RefreshExpiresAt);
    }
}
