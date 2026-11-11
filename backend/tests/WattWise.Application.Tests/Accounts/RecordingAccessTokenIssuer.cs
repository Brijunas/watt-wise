using NodaTime;

using WattWise.Application.Accounts;

namespace WattWise.Application.Tests.Accounts;

/// <summary>The JWT port: returns <c>jwt</c> and an expiry 15 minutes ahead, and records the claims it was asked for.</summary>
internal sealed class RecordingAccessTokenIssuer(List<string> calls) : IAccessTokenIssuer
{
    public IssuedAccessToken Issue(Guid accountId, Guid sessionId, IReadOnlyCollection<string> permissions, Instant now)
    {
        calls.Add($"Issue:{accountId}:{string.Join(',', permissions)}:{now}");
        SessionIds.Add(sessionId);
        return new IssuedAccessToken("jwt", now + Duration.FromMinutes(15));
    }

    public List<Guid> SessionIds { get; } = [];
}
