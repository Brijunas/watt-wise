using NodaTime;

namespace WattWise.Domain.Accounts;

/// <summary>One link in a session's chain of refresh tokens. Only its hash is kept.</summary>
public sealed class RefreshToken
{
    // Used by EF Core.
    private RefreshToken()
    {
    }

    private RefreshToken(Guid id, Guid sessionId, byte[] tokenHash, Instant issuedAt, Instant expiresAt)
    {
        Id = id;
        SessionId = sessionId;
        TokenHash = tokenHash;
        IssuedAt = issuedAt;
        ExpiresAt = expiresAt;
    }

    public Guid Id { get; private set; }

    public Guid SessionId { get; private set; }

    /// <summary>SHA-256 of the token.</summary>
    public byte[] TokenHash { get; private set; } = [];

    public Instant IssuedAt { get; private set; }

    public Instant ExpiresAt { get; private set; }

    /// <summary>When the token was exchanged for the next one; <c>null</c> while it is the session's current token.</summary>
    public Instant? UsedAt { get; private set; }

    internal static RefreshToken Create(Guid sessionId, byte[] tokenHash, Instant issuedAt, Instant expiresAt)
    {
        ArgumentOutOfRangeException.ThrowIfEqual(sessionId, Guid.Empty);
        ArgumentNullException.ThrowIfNull(tokenHash);
        ArgumentOutOfRangeException.ThrowIfZero(tokenHash.Length);
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(expiresAt, issuedAt);

        return new RefreshToken(Guid.CreateVersion7(), sessionId, tokenHash, issuedAt, expiresAt);
    }

    internal void MarkUsed(Instant now)
    {
        UsedAt = now;
    }
}
