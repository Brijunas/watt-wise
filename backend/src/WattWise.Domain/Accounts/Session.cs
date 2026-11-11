using System.Security.Cryptography;

using NodaTime;

namespace WattWise.Domain.Accounts;

/// <summary>
/// One sign-in on one device: the chain of refresh tokens that starts at that sign-in. Every refresh
/// exchanges the current token for a new one; presenting a token that was already exchanged is reuse.
/// </summary>
public sealed class Session
{
    private readonly List<RefreshToken> _refreshTokens = [];

    // Used by EF Core.
    private Session()
    {
    }

    private Session(Guid id, Guid accountId, string deviceLabel, string securityStamp, Instant signedInAt)
    {
        Id = id;
        AccountId = accountId;
        DeviceLabel = deviceLabel;
        SecurityStamp = securityStamp;
        SignedInAt = signedInAt;
        LastUsedAt = signedInAt;
    }

    public Guid Id { get; private set; }

    public Guid AccountId { get; private set; }

    public string DeviceLabel { get; private set; } = string.Empty;

    /// <summary>
    /// The account's security stamp when the password was checked. An opaque value: the session is stale once it no longer
    /// matches the account's current stamp, which a password change or reset replaces.
    /// </summary>
    public string SecurityStamp { get; private set; } = string.Empty;

    public Instant SignedInAt { get; private set; }

    public Instant LastUsedAt { get; private set; }

    public IReadOnlyCollection<RefreshToken> RefreshTokens => _refreshTokens;

    /// <summary>Starts a session whose first refresh token is <paramref name="tokenHash"/>.</summary>
    public static Session Start(Guid accountId, string deviceLabel, string securityStamp, byte[] tokenHash, Instant now, Instant expiresAt)
    {
        ArgumentOutOfRangeException.ThrowIfEqual(accountId, Guid.Empty);
        ArgumentException.ThrowIfNullOrWhiteSpace(deviceLabel);
        ArgumentException.ThrowIfNullOrWhiteSpace(securityStamp);

        Session session = new(Guid.CreateVersion7(), accountId, deviceLabel, securityStamp, now);
        session._refreshTokens.Add(RefreshToken.Create(session.Id, tokenHash, now, expiresAt));
        return session;
    }

    /// <summary>
    /// Exchanges the presented token for a new one. Throws <see cref="Exceptions.RefreshTokenReusedException"/> when the
    /// presented token was already exchanged and <see cref="Exceptions.RefreshTokenExpiredException"/> when it has expired.
    /// </summary>
    public void Rotate(byte[] presentedHash, byte[] newHash, Instant now, Instant expiresAt)
    {
        ArgumentNullException.ThrowIfNull(presentedHash);

        RefreshToken presented = _refreshTokens.SingleOrDefault(token => CryptographicOperations.FixedTimeEquals(token.TokenHash, presentedHash))
            ?? throw new ArgumentException("The token does not belong to this session.", nameof(presentedHash));

        if (presented.UsedAt is not null)
        {
            throw new Exceptions.RefreshTokenReusedException();
        }

        if (now >= presented.ExpiresAt)
        {
            throw new Exceptions.RefreshTokenExpiredException();
        }

        RefreshToken next = RefreshToken.Create(Id, newHash, now, expiresAt);
        presented.MarkUsed(now);
        _refreshTokens.Add(next);
        LastUsedAt = now;
    }
}
