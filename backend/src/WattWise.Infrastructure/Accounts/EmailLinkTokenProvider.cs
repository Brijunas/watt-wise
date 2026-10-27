using System.Buffers.Text;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;

using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Storage;

using NodaTime;

using WattWise.Infrastructure.Persistence;

namespace WattWise.Infrastructure.Accounts;

/// <summary>
/// Identity's token provider for the email confirmation and password reset links. A token is 32
/// random bytes, Base64Url-encoded; only its SHA-256 hash is stored, one row per identity and
/// purpose, so a new link replaces the old one in a single upsert. Checking a token marks its row deleted without
/// saving: Identity saves that in the same <c>SaveChanges</c> as the confirmation or the new
/// password, so a link works once. Anything else saving in the same scope after Identity reports a
/// failure would also save the deletion and use up the link.
/// </summary>
public sealed class EmailLinkTokenProvider(AppDbContext context, IClock clock) : IUserTwoFactorTokenProvider<AccountIdentity>
{
    public const string ProviderName = "EmailLink";

    private const int TokenByteCount = 32;

    // Identity's interface carries no CancellationToken, and UserManager's own is protected and
    // always CancellationToken.None, so there is nothing to forward: Identity itself calls its store with None.
    private static readonly CancellationToken NoCancellation = CancellationToken.None;

    public async Task<string> GenerateAsync(string purpose, UserManager<AccountIdentity> manager, AccountIdentity user)
    {
        Duration lifetime = LifetimeOf(purpose);

        byte[] raw = RandomNumberGenerator.GetBytes(TokenByteCount);
        byte[] hash = SHA256.HashData(raw);
        Instant now = clock.GetCurrentInstant();

        // One statement, so two requests for the same identity and purpose (a double-clicked "resend")
        // cannot both insert and fail on the primary key: the later one replaces the earlier link.
        // Written at once, not with Identity's next update: the link goes out by email. It bypasses the
        // change tracker, which only matters if this scope already loaded the row by checking a link; no
        // flow generates a link after checking one in the same scope.
        await context.Database.ExecuteSqlAsync(UpsertSql(hash, user.Id, purpose, now, now + lifetime), NoCancellation);

        return Base64Url.EncodeToString(raw);
    }

    public async Task<bool> ValidateAsync(string purpose, string token, UserManager<AccountIdentity> manager, AccountIdentity user)
    {
        LifetimeOf(purpose);

        if (!TryDecode(token, out byte[] raw))
        {
            return false;
        }

        EmailLinkToken? row = await context.EmailLinkTokens.FindAsync([user.Id, purpose], NoCancellation);
        if (row is null || clock.GetCurrentInstant() >= row.ExpiresAt)
        {
            return false;
        }

        if (!CryptographicOperations.FixedTimeEquals(row.TokenHash, SHA256.HashData(raw)))
        {
            return false;
        }

        // Not saved here: see the type summary. Of two uses at once, the second deletes 0 rows and
        // EF raises a concurrency exception, which UserStore turns into ConcurrencyFailure.
        context.EmailLinkTokens.Remove(row);
        return true;
    }

    /// <summary>Not a sign-in factor.</summary>
    public Task<bool> CanGenerateTwoFactorTokenAsync(UserManager<AccountIdentity> manager, AccountIdentity user)
    {
        return Task.FromResult(false);
    }

    /// <summary>
    /// The upsert, with the table and column names read from the EF model (so they stay what
    /// <c>EmailLinkTokenConfiguration</c> and the naming convention say) and every value a parameter.
    /// </summary>
    private FormattableString UpsertSql(byte[] hash, Guid identityId, string purpose, Instant createdAt, Instant expiresAt)
    {
        IEntityType entity = context.Model.FindEntityType(typeof(EmailLinkToken))
            ?? throw new InvalidOperationException("EmailLinkToken is not in the model.");
        StoreObjectIdentifier table = StoreObjectIdentifier.Create(entity, StoreObjectType.Table)
            ?? throw new InvalidOperationException("EmailLinkToken is not mapped to a table.");
        ISqlGenerationHelper sql = context.GetService<ISqlGenerationHelper>();

        string Column(string property) => sql.DelimitIdentifier(
            entity.FindProperty(property)?.GetColumnName(table) ?? throw new InvalidOperationException($"{property} is not mapped."));

        string identity = Column(nameof(EmailLinkToken.IdentityId));
        string purposeColumn = Column(nameof(EmailLinkToken.Purpose));
        string hashColumn = Column(nameof(EmailLinkToken.TokenHash));
        string expiresColumn = Column(nameof(EmailLinkToken.ExpiresAt));
        string createdColumn = Column(nameof(EmailLinkToken.CreatedAt));

        // The names above come from the model, never from a request; the values go in as parameters.
        return FormattableStringFactory.Create(
            $$"""
            INSERT INTO {{sql.DelimitIdentifier(table.Name, table.Schema)}} ({{identity}}, {{purposeColumn}}, {{hashColumn}}, {{expiresColumn}}, {{createdColumn}})
            VALUES ({0}, {1}, {2}, {3}, {4})
            ON CONFLICT ({{identity}}, {{purposeColumn}}) DO UPDATE SET
                {{hashColumn}} = EXCLUDED.{{hashColumn}},
                {{expiresColumn}} = EXCLUDED.{{expiresColumn}},
                {{createdColumn}} = EXCLUDED.{{createdColumn}}
            """,
            identityId, purpose, hash, expiresAt, createdAt);
    }

    private static Duration LifetimeOf(string purpose)
    {
        return purpose switch
        {
            UserManager<AccountIdentity>.ConfirmEmailTokenPurpose => Duration.FromHours(24),
            UserManager<AccountIdentity>.ResetPasswordTokenPurpose => Duration.FromHours(1),
            _ => throw new NotSupportedException($"Email links are not issued for the purpose '{purpose}'."),
        };
    }

    /// <summary>
    /// Decodes a token of exactly 32 bytes in its canonical Base64Url form. <c>TryDecodeFromChars</c> throws on
    /// malformed input, so it runs only after <c>IsValid</c>. Base64Url ignores the unused bits of its last
    /// character, so without the re-encoding check a token with a changed last character would still be accepted.
    /// </summary>
    private static bool TryDecode(string token, out byte[] raw)
    {
        raw = new byte[TokenByteCount];
        return Base64Url.IsValid(token, out int length)
            && length == TokenByteCount
            && Base64Url.TryDecodeFromChars(token, raw, out int written)
            && written == TokenByteCount
            && string.Equals(Base64Url.EncodeToString(raw), token, StringComparison.Ordinal);
    }
}
