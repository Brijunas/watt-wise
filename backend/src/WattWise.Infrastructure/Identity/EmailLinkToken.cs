using NodaTime;

namespace WattWise.Infrastructure.Identity;

/// <summary>
/// The live email link of one kind (Identity's purpose string) for one identity. Only the SHA-256
/// hash of the token is stored. Sending a new link replaces the row, using a link deletes it.
/// </summary>
public sealed class EmailLinkToken
{
    public required Guid IdentityId { get; init; }

    public required string Purpose { get; init; }

    /// <summary>SHA-256 of the token's 32 random bytes.</summary>
    public required byte[] TokenHash { get; set; }

    public required Instant ExpiresAt { get; set; }

    public required Instant CreatedAt { get; set; }
}
