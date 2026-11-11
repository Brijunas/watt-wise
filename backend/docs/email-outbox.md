# Email outbox

How an email gets from a use case to SMTP. The Api never sends: a use case records an email request in `app.email_requests` in the same `SaveChanges` as its account change, and the Jobs host sends it. A request never touches SMTP, so its timing can't show whether an address has an account ([security.md](../../docs/security.md#accounts)). The port, messages, rendering and SMTP settings are in [email.md](email.md); the job classes in [jobs.md](jobs.md).

## Writing a request

A command handler calls Application's `Email/IEmailRequests.Add(...)` itself; there are no domain events and no interceptor. An address is normalized with the `EmailLookupNormalizer` rules.

| Flow                   | Account change                                  | Request                                             |
| ---------------------- | ----------------------------------------------- | --------------------------------------------------- |
| Register, new email    | creates the `Account` and its `AccountIdentity` | `EmailConfirmation` for the new account             |
| Register, taken email  | none; the password is still hashed and dropped  | `AccountAlreadyExists` for the existing account     |
| Resend confirmation    | none                                            | `EmailConfirmation` keyed by the normalized address |
| Forgot password (S4.5) | none                                            | `PasswordReset` keyed by the normalized address     |

If two registrations race for the same new address, the second gets the same 202 and no email; the first one's request already sends it.

Resend and forgot-password never check whether the address has an account; the worker does.

## The table

`app.email_requests`, mapped by Infrastructure's `Email/EmailRequest` (an outbox row is a technical detail, not a Domain entity, per [domain-model.md](domain-model.md)).

| Column            | Type               | Notes                                                                                         |
| ----------------- | ------------------ | --------------------------------------------------------------------------------------------- |
| `id`              | `uuid`             | `Guid` v7                                                                                     |
| `kind`            | `text`             | `EmailConfirmation`, `AccountAlreadyExists` or `PasswordReset`                                |
| `account_id`      | `uuid` null        | FK to `accounts`, on delete cascade                                                           |
| `email`           | `text` null        | normalized address; exactly one of `account_id` and `email` is set (`CHECK`)                  |
| `status`          | `text`             | `pending`, `sending`, `sent`, `superseded` or `failed` (`CHECK`)                              |
| `attempt_count`   | `integer`          | default 0                                                                                     |
| `next_attempt_at` | `timestamptz`      | now on insert                                                                                 |
| `last_attempt_at` | `timestamptz` null |                                                                                               |
| `last_error`      | `text` null        | an exception type name or an SMTP status (`SmtpCommandException 451`), at most 100 characters |
| `created_at`      | `timestamptz`      |                                                                                               |
| `sent_at`         | `timestamptz` null |                                                                                               |

- **Never stored:** a token, a link, or a server's message. The token's SHA-256 hash lives in `email_link_tokens`.
- **Index:** `ix_email_requests_due` on (`status`, `next_attempt_at`) where `status` is `pending` or `sending`.
- **Statuses:** `pending` → `sending` → `sent`; a retry goes back to `pending` with a later `next_attempt_at`. `sent`, `superseded` and `failed` are final.
- **Access:** the `api` role inserts rows; the `jobs` role reads, updates and deletes them (it reads and writes `app`, [postgres.md](../../deploy/docs/postgres.md)).

## Each pass

`SendEmailsJob` sends `SendDueEmailsCommand` every 5 seconds. The handler owns the order of steps and the retry decision; the SQL, the lookups and the link creation sit behind `IEmailOutbox` in Infrastructure.

1. **Recover stuck rows.** A `sending` row whose `last_attempt_at` is older than 5 minutes (the 2-minute SMTP timeout plus a margin) goes back to `pending` with `last_error = 'SendingTimedOut'` if it has attempts left; if it already timed out once or has none left, it becomes `failed`. At worst the user gets two emails, and the newer link replaces the older.
2. **Claim**, in one transaction with steps 3 to 6 that commits before any SMTP traffic: an `UPDATE … FROM (SELECT … FOR UPDATE SKIP LOCKED LIMIT 20)` that moves due `pending` rows to `sending`, adds one to `attempt_count` and sets `last_attempt_at`. The time is `IClock`'s. The claim, not Hangfire, is what stops two passes from sending one row.
3. **Coalesce.** For each kind and account (or address) only the newest request, by `created_at` then `id`, survives; older `pending` and just-claimed rows become `superseded`. After step 4, rows that resolved to the same account are coalesced again the same way, so a resend by address and a registration for that account send one email. A request not yet due under the other key can still send later.
4. **Resolve** each row to an account and its preferred language. The row is deleted and nothing is sent for an unknown address, a confirmation for an account that is already confirmed, and a password reset for an account that isn't confirmed.
5. **Create the token** through `EmailLinkTokenProvider` (only its hash is stored; the new link replaces the old). `AccountAlreadyExists` has none.
6. **Build the link** in memory: `<Links:FrontendBaseUrl>/confirm-email?id=<AccountIdentityId>&token=<base64url>` or `/reset-password?id=…&token=…`.
7. **Send** through `IEmailSender` in the account's language, with the Message-Id local part `<row id N>.<attempt>`, so every attempt has its own Message-Id.
8. **Record** in a second short transaction: `sent` with `sent_at`, or the retry decision.

A bug while handling a row (a broken template, a missing setting) marks that row `failed` with the exception's type name and logs it at Error; the other rows go on. Cancellation leaves the row in `sending` for step 1.

No new send starts more than 3 minutes after the claim (the stuck threshold minus the SMTP timeout); rows a slow pass didn't reach stay `sending` and step 1 returns them. Recording only updates a row that is still `sending` at the same attempt, so a late result never overwrites a row that has moved on.

## Retry policy

`EmailRetryPolicy` decides without I/O from `Email:Retry:<Kind>` in the Jobs host's `appsettings.json`: `Attempts` and `Delays`, one `TimeSpan` per attempt, the delay before it. Validated at start: `Delays` has `Attempts` entries, none negative, the first 0.

| Kind                   | Attempts | Delays (minutes)     | Why                     |
| ---------------------- | -------- | -------------------- | ----------------------- |
| `PasswordReset`        | 4        | 0, 1, 5, 15          | its link lasts 1 hour   |
| `EmailConfirmation`    | 6        | 0, 1, 5, 15, 60, 240 | its link lasts 24 hours |
| `AccountAlreadyExists` | 3        | 0, 5, 30             | no link                 |

A `Retryable` result with attempts left goes back to `pending` at now plus the next delay, with `last_error` set. A `Permanent` result, or the last attempt, is `failed` and logged at Error with the kind and language only.

## Purge

`PurgeEmailRequestsJob` runs daily at 03:00 UTC and deletes `sent`, `superseded` and `failed` rows created more than 30 days ago.

## Testing

- **Application.Tests:** `EmailRetryPolicy` for each kind and its last attempt.
- **Infrastructure.IntegrationTests (`Email/`):** the outbox with seeded rows, `FakeClock` and a capturing fake `IEmailSender`: claiming (two passes never claim one row twice), coalescing, the three drop rules, the hash-only token and the exact link, the language, 4xx retried and 5xx failed, stuck rows once and twice, the purge, and no plaintext token in any table.
- **Jobs.IntegrationTests:** both recurring jobs are registered with their cron.
- **Api.IntegrationTests** stop at the row and never read an email.
