using System.Data.Common;

using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Options;

using NodaTime;

using WattWise.Application.Accounts;
using WattWise.Application.Email;
using WattWise.Domain.Localization;
using WattWise.Infrastructure.Accounts;
using WattWise.Infrastructure.Persistence;

namespace WattWise.Infrastructure.Email;

/// <summary>
/// The worker's side of <c>app.email_requests</c>: claiming, coalescing, resolving, link creation, recording,
/// stuck-row recovery and the purge. Coalescing runs twice: by kind and key in SQL, then by kind and resolved account. Every write is a single statement; the change tracker is cleared when a row fails mid-preparation.
/// </summary>
internal sealed class EmailOutbox(
    AppDbContext db,
    UserManager<AccountIdentity> users,
    IClock clock,
    IOptions<LinkSettings> links) : IEmailOutbox
{
    private const int MaxErrorLength = 100;
    private const string TimedOut = "SendingTimedOut";

    // The claim is a compare-and-set UPDATE; SKIP LOCKED lets passes running at once take different rows.
    private const string ClaimSql = """
        WITH due AS (
          SELECT id FROM app.email_requests
          WHERE status = 'pending' AND next_attempt_at <= @now
          ORDER BY next_attempt_at LIMIT @batch
          FOR UPDATE SKIP LOCKED)
        UPDATE app.email_requests r
        SET status = 'sending', attempt_count = r.attempt_count + 1, last_attempt_at = @now
        FROM due WHERE r.id = due.id AND r.status = 'pending'
        RETURNING r.id
        """;

    public async Task<int> RecoverStuckAsync(
        Duration stuckAfter, IReadOnlyDictionary<EmailRequestKind, int> maxAttempts, CancellationToken cancellationToken)
    {
        Instant now = clock.GetCurrentInstant();
        Instant cutoff = now - stuckAfter;
        string[] kinds = [.. maxAttempts.Keys.Select(kind => kind.ToString())];
        int[] limits = [.. maxAttempts.Values];

        return await db.Database.ExecuteSqlAsync(
            $"""
            UPDATE app.email_requests r
            SET status = CASE WHEN r.last_error = {TimedOut} OR r.attempt_count >= m.max_attempts THEN 'failed' ELSE 'pending' END,
                last_error = {TimedOut},
                next_attempt_at = CASE WHEN r.last_error = {TimedOut} OR r.attempt_count >= m.max_attempts THEN r.next_attempt_at ELSE {now} END
            FROM unnest({kinds}::text[], {limits}::integer[]) AS m(kind, max_attempts)
            WHERE r.kind = m.kind AND r.status = 'sending' AND r.last_attempt_at < {cutoff}
            """,
            cancellationToken);
    }

    public async Task<IReadOnlyList<ReadyEmail>> ClaimAsync(int batchSize, CancellationToken cancellationToken)
    {
        await using IDbContextTransaction transaction = await db.Database.BeginTransactionAsync(cancellationToken);

        List<Guid> claimedIds = await ClaimDueAsync(transaction, batchSize, cancellationToken);
        if (claimedIds.Count == 0)
        {
            await transaction.CommitAsync(cancellationToken);
            return [];
        }

        await SupersedeOlderAsync([.. claimedIds], cancellationToken);

        List<EmailRequest> claimed = await db.EmailRequests.AsNoTracking()
            .Where(request => claimedIds.Contains(request.Id) && request.Status == EmailRequestStatus.Sending)
            .OrderBy(request => request.NextAttemptAt).ThenBy(request => request.Id)
            .ToListAsync(cancellationToken);

        List<ReadyEmail> ready = [];
        List<Guid> dropped = [];

        // Resolve every claimed row to its identity first, so rows keyed by address and rows keyed by account
        // for the same account can be coalesced before any token is created.
        List<(EmailRequest Request, AccountIdentity Identity)> resolved = [];
        foreach (EmailRequest request in claimed)
        {
            AccountIdentity? identity = request.AccountId is { } accountId
                ? await users.Users.SingleOrDefaultAsync(user => user.AccountId == accountId, cancellationToken)
                : await users.FindByEmailAsync(request.Email!);
            if (IsDropped(request, identity))
            {
                dropped.Add(request.Id);
            }
            else
            {
                resolved.Add((request, identity!));
            }
        }

        // Second coalesce, by kind and resolved account: only the newest claimed row survives. Rows that were
        // not claimed in this pass are untouched.
        List<(EmailRequest Request, AccountIdentity Identity)> survivors = [];
        List<Guid> superseded = [];
        foreach (IGrouping<(EmailRequestKind, Guid), (EmailRequest Request, AccountIdentity Identity)> group in
            resolved.GroupBy(item => (item.Request.Kind, item.Identity.AccountId)))
        {
            // Postgres orders uuids by their bytes, so compare the big-endian bytes, as the first coalesce does.
            List<(EmailRequest Request, AccountIdentity Identity)> ordered = [.. group
                .OrderByDescending(item => item.Request.CreatedAt)
                .ThenByDescending(item => item.Request.Id.ToByteArray(bigEndian: true), ByteArrayComparer.Instance)];
            survivors.Add(ordered[0]);
            superseded.AddRange(ordered.Skip(1).Select(item => item.Request.Id));
        }

        if (superseded.Count > 0)
        {
            await db.EmailRequests.Where(request => superseded.Contains(request.Id))
                .ExecuteUpdateAsync(set => set.SetProperty(request => request.Status, EmailRequestStatus.Superseded), cancellationToken);
        }

        foreach ((EmailRequest request, AccountIdentity identity) in survivors.OrderBy(item => item.Request.NextAttemptAt).ThenBy(item => item.Request.Id))
        {
            try
            {
                ready.Add(await BuildAsync(request, identity, cancellationToken));
            }
            catch (Exception exception) when (exception is not (OperationCanceledException or DbException))
            {
                // A bug while preparing this row (a missing setting): it fails alone and the rest go on. A half-done
                // token write may still be tracked in the shared context, so forget it before the next row saves.
                db.ChangeTracker.Clear();
                await MarkFailedAsync(request.Id, request.AttemptCount, exception.GetType().Name, cancellationToken);
            }
        }

        if (dropped.Count > 0)
        {
            await db.EmailRequests.Where(request => dropped.Contains(request.Id)).ExecuteDeleteAsync(cancellationToken);
        }

        await transaction.CommitAsync(cancellationToken);
        return ready;
    }

    public async Task<bool> RecordSentAsync(Guid requestId, int attempt, CancellationToken cancellationToken)
    {
        Instant now = clock.GetCurrentInstant();
        return await Current(requestId, attempt)
            .ExecuteUpdateAsync(
                set => set.SetProperty(request => request.Status, EmailRequestStatus.Sent).SetProperty(request => request.SentAt, now),
                cancellationToken) > 0;
    }

    public async Task<bool> RecordRetryAsync(Guid requestId, int attempt, Instant nextAttemptAt, string error, CancellationToken cancellationToken)
    {
        string lastError = Truncate(error);
        return await Current(requestId, attempt)
            .ExecuteUpdateAsync(
                set => set
                    .SetProperty(request => request.Status, EmailRequestStatus.Pending)
                    .SetProperty(request => request.NextAttemptAt, nextAttemptAt)
                    .SetProperty(request => request.LastError, lastError),
                cancellationToken) > 0;
    }

    public Task<bool> RecordFailedAsync(Guid requestId, int attempt, string error, CancellationToken cancellationToken) =>
        MarkFailedAsync(requestId, attempt, error, cancellationToken);

    /// <summary>The row, only while it is still the given attempt in <c>sending</c>.</summary>
    private IQueryable<EmailRequest> Current(Guid requestId, int attempt) =>
        db.EmailRequests.Where(request =>
            request.Id == requestId && request.Status == EmailRequestStatus.Sending && request.AttemptCount == attempt);

    public async Task<int> PurgeAsync(Instant createdBefore, CancellationToken cancellationToken)
    {
        EmailRequestStatus[] finished = [EmailRequestStatus.Sent, EmailRequestStatus.Superseded, EmailRequestStatus.Failed];
        return await db.EmailRequests
            .Where(request => finished.Contains(request.Status) && request.CreatedAt < createdBefore)
            .ExecuteDeleteAsync(cancellationToken);
    }

    private async Task<List<Guid>> ClaimDueAsync(IDbContextTransaction transaction, int batchSize, CancellationToken cancellationToken)
    {
        await using DbCommand command = db.Database.GetDbConnection().CreateCommand();
        command.Transaction = transaction.GetDbTransaction();
        command.CommandText = ClaimSql;
        AddParameter(command, "now", clock.GetCurrentInstant().ToDateTimeOffset());
        AddParameter(command, "batch", batchSize);

        List<Guid> ids = [];
        await using DbDataReader reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            ids.Add(reader.GetGuid(0));
        }

        return ids;
    }

    private static void AddParameter(DbCommand command, string name, object value)
    {
        DbParameter parameter = command.CreateParameter();
        parameter.ParameterName = name;
        parameter.Value = value;
        command.Parameters.Add(parameter);
    }

    /// <summary>
    /// Only the newest request (by created_at, then id) of each kind and account or address survives. Older pending
    /// and just-claimed rows of the groups the claim touched become superseded.
    /// </summary>
    private async Task SupersedeOlderAsync(Guid[] claimedIds, CancellationToken cancellationToken)
    {
        await db.Database.ExecuteSqlAsync(
            $"""
            UPDATE app.email_requests SET status = 'superseded'
            WHERE id IN (
              SELECT id FROM (
                SELECT r.id, row_number() OVER (PARTITION BY r.kind, r.account_id, r.email ORDER BY r.created_at DESC, r.id DESC) AS rn
                FROM app.email_requests r
                WHERE (r.status = 'pending' OR r.id = ANY({claimedIds}))
                  AND EXISTS (
                    SELECT 1 FROM app.email_requests c
                    WHERE c.id = ANY({claimedIds}) AND c.kind = r.kind
                      AND c.account_id IS NOT DISTINCT FROM r.account_id AND c.email IS NOT DISTINCT FROM r.email)
              ) ranked WHERE rn > 1)
            """,
            cancellationToken);
    }

    /// <summary>Nothing is sent for an unknown address, a confirmation for a confirmed account, or a reset for an unconfirmed one.</summary>
    private static bool IsDropped(EmailRequest request, AccountIdentity? identity) =>
        identity is null
        || (request.Kind == EmailRequestKind.EmailConfirmation && identity.EmailConfirmed)
        || (request.Kind == EmailRequestKind.PasswordReset && !identity.EmailConfirmed);

    /// <summary>Builds the message of a surviving row, creating its link token.</summary>
    private async Task<ReadyEmail> BuildAsync(EmailRequest request, AccountIdentity identity, CancellationToken cancellationToken)
    {
        Language language = await db.Accounts
            .Where(account => account.Id == identity.AccountId)
            .Select(account => account.PreferredLanguage)
            .SingleAsync(cancellationToken);
        string to = identity.Email!;

        EmailMessage message = request.Kind switch
        {
            EmailRequestKind.EmailConfirmation => new EmailConfirmationEmail(
                to, language, LinkTo("confirm-email", identity.Id, await users.GenerateEmailConfirmationTokenAsync(identity))),
            EmailRequestKind.PasswordReset => new PasswordResetEmail(
                to, language, LinkTo("reset-password", identity.Id, await users.GeneratePasswordResetTokenAsync(identity))),
            EmailRequestKind.AccountAlreadyExists => new AccountAlreadyExistsEmail(to, language),
            _ => throw new InvalidOperationException($"Unknown email kind {request.Kind}."),
        };
        return new ReadyEmail(request.Id, request.Kind, request.AttemptCount, message);
    }

    private Uri LinkTo(string path, Guid identityId, string token) =>
        new($"{links.Value.FrontendBaseUrl}/{path}?id={identityId}&token={Uri.EscapeDataString(token)}");

    private async Task<bool> MarkFailedAsync(Guid requestId, int attempt, string error, CancellationToken cancellationToken)
    {
        string lastError = Truncate(error);
        return await Current(requestId, attempt)
            .ExecuteUpdateAsync(
                set => set.SetProperty(request => request.Status, EmailRequestStatus.Failed).SetProperty(request => request.LastError, lastError),
                cancellationToken) > 0;
    }

    private static string Truncate(string error) => error.Length <= MaxErrorLength ? error : error[..MaxErrorLength];

    private sealed class ByteArrayComparer : IComparer<byte[]>
    {
        public static readonly ByteArrayComparer Instance = new();

        public int Compare(byte[]? x, byte[]? y) => x.AsSpan().SequenceCompareTo(y.AsSpan());
    }
}
