using NodaTime;

namespace WattWise.Application.Accounts;

/// <summary>Signs access tokens (JWTs).</summary>
public interface IAccessTokenIssuer
{
    /// <summary>Issues a token for the account and session, valid for <see cref="TokenLifetimes.AccessToken"/> from <paramref name="now"/>.</summary>
    IssuedAccessToken Issue(Guid accountId, Guid sessionId, IReadOnlyCollection<string> permissions, Instant now);
}
