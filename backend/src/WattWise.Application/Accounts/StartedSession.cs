using NodaTime;

using WattWise.Domain.Accounts;

namespace WattWise.Application.Accounts;

internal sealed record StartedSession(Session Session, NewRefreshToken RefreshToken, Instant RefreshExpiresAt)
{
    // A record would print the token.
    public override string ToString() => nameof(StartedSession);
}
