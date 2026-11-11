using NodaTime;

using WattWise.Domain.Accounts;

namespace WattWise.Application.Accounts;

internal static class SessionStarting
{
    /// <summary>
    /// Stages a new session with its first refresh token; the caller saves. <paramref name="securityStamp"/> is the stamp the
    /// password was checked against, which refresh compares with the account's current one.
    /// </summary>
    public static StartedSession Start(
        ISessionRepository sessions,
        IRefreshTokenFactory refreshTokens,
        IDeviceLabels deviceLabels,
        Guid accountId,
        string? userAgent,
        string securityStamp,
        Instant now)
    {
        NewRefreshToken refreshToken = refreshTokens.Create();
        Instant refreshExpiresAt = now + TokenLifetimes.RefreshToken;
        Session session = Session.Start(accountId, deviceLabels.FromUserAgent(userAgent), securityStamp, refreshToken.Hash, now, refreshExpiresAt);
        sessions.Add(session);
        return new StartedSession(session, refreshToken, refreshExpiresAt);
    }
}
