using Mediator;

using NodaTime;

using WattWise.Application.Results;
using WattWise.Domain.Accounts;
using WattWise.Domain.Exceptions;

namespace WattWise.Application.Accounts;

public sealed class RefreshSessionCommandHandler(
    IRefreshTokenFactory refreshTokens,
    ISessionRepository sessions,
    IAccountsQueries queries,
    IAccessTokenIssuer accessTokens,
    IUnitOfWork unitOfWork,
    IClock clock) : ICommandHandler<RefreshSessionCommand, Result<SignedIn>>
{
    public async ValueTask<Result<SignedIn>> Handle(RefreshSessionCommand command, CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(command.RefreshToken))
        {
            return AccountErrors.InvalidRefreshToken();
        }

        byte[] presentedHash = refreshTokens.Hash(command.RefreshToken);
        Session? session = await sessions.GetByRefreshTokenHashAsync(presentedHash, cancellationToken);
        if (session is null)
        {
            return AccountErrors.InvalidRefreshToken();
        }

        // A password change or reset replaced the stamp the session was started with: the session is stale, and it ends.
        string? currentStamp = await queries.GetSecurityStampAsync(session.AccountId, cancellationToken);
        if (currentStamp is null || !string.Equals(currentStamp, session.SecurityStamp, StringComparison.Ordinal))
        {
            await SessionEnding.EndAsync(sessions, unitOfWork, presentedHash, cancellationToken);
            return AccountErrors.InvalidRefreshToken();
        }

        // A locked-out account can still refresh: the lockout stops password guessing, and a session is proof of an earlier right password.
        Instant now = clock.GetCurrentInstant();
        NewRefreshToken next = refreshTokens.Create();
        Instant refreshExpiresAt = now + TokenLifetimes.RefreshToken;
        try
        {
            session.Rotate(presentedHash, next.Hash, now, refreshExpiresAt);
        }
        catch (RefreshTokenReusedException)
        {
            // A used token coming back means it leaked: end the session, so the token the thief or the owner holds now stops too.
            await SessionEnding.EndAsync(sessions, unitOfWork, presentedHash, cancellationToken);
            return AccountErrors.InvalidRefreshToken();
        }
        catch (RefreshTokenExpiredException)
        {
            return AccountErrors.InvalidRefreshToken();
        }

        IReadOnlyList<string> permissions = await queries.GetPermissionsAsync(session.AccountId, cancellationToken);
        try
        {
            await unitOfWork.SaveChangesAsync(cancellationToken);
        }
        catch (ConcurrencyConflictException)
        {
            // Another request exchanged the same token first, so by now it was used: reuse, which ends the session.
            await SessionEnding.EndAsync(sessions, unitOfWork, presentedHash, cancellationToken);
            return AccountErrors.InvalidRefreshToken();
        }

        IssuedAccessToken accessToken = accessTokens.Issue(session.AccountId, session.Id, permissions, now);
        return new SignedIn(accessToken.Token, accessToken.ExpiresAt, next.Token, refreshExpiresAt);
    }
}
