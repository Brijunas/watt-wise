# Accounts

The account is the user: its id, preferred language and creation time, and everything the user owns hangs off it. This doc is how the feature is built: the model, ASP.NET Core Identity, the emails it sends and the account flows: registration and confirmation (S4.3), then sign-in and the rest. Each E4 story adds its section here. What the feature enforces (passwords, lockout, tokens, sessions, permissions) is decided in the root [security.md](../../../docs/security.md); the token flow seen by clients is in the root [technical.md](../../../docs/technical.md#backend).

## Vocabulary

| Write                                                   | Meaning                                                                                                                               | Don't write                                                            |
| ------------------------------------------------------- | ------------------------------------------------------------------------------------------------------------------------------------- | ---------------------------------------------------------------------- |
| account; `Accounts` for folders, namespaces and the doc | the user's record and the feature                                                                                                     | "identity", "Identities", or "user" for the record                     |
| `AccountIdentity`                                       | the account's email-and-password sign-in record (ASP.NET Core Identity's user type), one per account; OAuth logins attach to it later | "identity" on its own; the id that points at it is `AccountIdentityId` |
| ASP.NET Core Identity                                   | the library                                                                                                                           | "Identity" on its own                                                  |
| sign up, sign in, sign out                              | what the user does                                                                                                                    | "log in", "login" (except ASP.NET Core Identity's "external logins")   |
| `/api/v1/auth` endpoints                                | the route group                                                                                                                       | "auth endpoints", "auth" on its own                                    |
| role, permission                                        | what an account may do                                                                                                                |                                                                        |
| session                                                 | one sign-in on one device, ended by sign-out                                                                                          |                                                                        |

## Where the code lives

The feature's word is `Accounts` in every project ([architecture.md](../architecture.md#folders)).

| Project                                                                                | Where                         | What                                                                                                                                                                                                                                                                                                                                                                                                  |
| -------------------------------------------------------------------------------------- | ----------------------------- | ----------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| Domain                                                                                 | `Accounts/`                   | `Account`, `AccountRole`, `Role`, `RolePermission`, `Permission`                                                                                                                                                                                                                                                                                                                                      |
| Application                                                                            | `Accounts/`                   | The email records (`EmailConfirmationEmail`, `AccountAlreadyExistsEmail`, `PasswordResetEmail`); `RegisterAccountCommand`, `ConfirmEmailCommand`, `ResendConfirmationCommand`, their validators and the ports they use                                                                                                                                                                                |
| Application, Infrastructure                                                            | `Email/`                      | The email concept, shared by every feature: Application's `IEmailSender` and `EmailMessage`, Infrastructure's `SmtpEmailSender`, `EmailRenderer`, `SmtpSettings` and the templates ([email.md](../email.md)), and the outbox: `IEmailRequests`, `IEmailOutbox`, `EmailRetryPolicy`, `SendDueEmailsCommand`, Infrastructure's `EmailRequest` and `EmailOutbox` ([email-outbox.md](../email-outbox.md)) |
| Infrastructure                                                                         | `Accounts/`                   | The ASP.NET Core Identity adapter: `AccountIdentity`, `EmailLinkToken` and `EmailLinkTokenProvider`, `EmailLookupNormalizer`, `NormalizingPasswordHasher`, the password validators                                                                                                                                                                                                                    |
| Infrastructure                                                                         | `Persistence/Configurations/` | The EF mappings (`AccountConfiguration`, `AccountIdentityConfiguration`, …), in the folder every feature shares                                                                                                                                                                                                                                                                                       |
| Api                                                                                    | `Endpoints/`                  | `AccountsEndpointModule`. It maps the `/api/v1/auth` route group; the public name stays `auth`, like the shared i18n namespace ([frontend/docs/structure.md](../../../frontend/docs/structure.md#features))                                                                                                                                                                                           |
| Jobs                                                                                   | `Recurring/`                  | `SendEmailsJob` and `PurgeEmailRequestsJob`                                                                                                                                                                                                                                                                                                                                                           |
| Domain.Tests, Application.Tests, Infrastructure.IntegrationTests, Api.IntegrationTests | `Accounts/`                   | The feature's tests. `AccountsTestSupport` builds services for the `api` role, with an optional `FakeClock`, and makes accounts, `AccountIdentity` records and unique emails                                                                                                                                                                                                                          |

## Emails

The account flows send three messages, each in the account's preferred language (LT or EN). They are records in Application's `Accounts/`; S4.2 built the sending, the stories below build and send them ([email.md](../email.md)).

| Message                     | What it says                                                                         | Requested by                             |
| --------------------------- | ------------------------------------------------------------------------------------ | ---------------------------------------- |
| `EmailConfirmationEmail`    | Confirm your email with this link, valid 24 hours; ignore this if you didn't sign up | S4.3, on registration and on resend      |
| `AccountAlreadyExistsEmail` | Someone tried to sign up with your email, which already has an account; no link      | S4.3, on registration with a taken email |
| `PasswordResetEmail`        | Set a new password with this link, valid 1 hour; ignore this if you didn't ask       | S4.5, on forgot password                 |

A use case only records the request; the Jobs host sends it ([email-outbox.md](../email-outbox.md)). Link lifetimes and the no-account-discovery rule are in [security.md](../../../docs/security.md#accounts).

## Model

ASP.NET Core Identity in Infrastructure, on `AppDbContext`, handles the account's `AccountIdentity`: password hashing, lockout, email tokens and (later) external OAuth logins.

- **Four groups of tables** in the `app` schema:
  - **Account:** `accounts` (Domain `Accounts/Account`).
  - **Authentication:** `identity_users` (one per account, `account_id` unique), `identity_user_logins` and `identity_user_tokens` (kept for OAuth, unused), `email_link_tokens` (one per `AccountIdentity` and purpose).
  - **Authorization:** `roles`, `account_roles`, `permissions`, `role_permissions` (Domain `Accounts/Role`, `Permission`, and the link entities `AccountRole`, `RolePermission`).
  - **Email requests:** `email_requests`, the outbox ([email-outbox.md](../email-outbox.md#the-table)).

  `AppDbContext` derives from `IdentityUserContext`, so ASP.NET Core Identity's own role and claim tables are not in the model.

  `account_roles` and `role_permissions` hold ids: `Account` keeps its roles as `AccountRole` links and exposes `RoleIds`, `Role` keeps `RolePermission` links and exposes `PermissionIds`; neither has a navigation to the other aggregate ([domain-model.md](../domain-model.md#aggregates)). What an account's roles grant is read by a query (S4.4), never by loading roles.

## ASP.NET Core Identity

- **Registration.** `AddInfrastructure()` registers ASP.NET Core Identity (its private `AddIdentity()`), its options and the email-link token provider, so every host has it with the same rules; only the Api uses it today.
- **Store options.** `IdentityOptions.Stores` stays at its defaults: ASP.NET Core Identity reads it from the app's services while building the model, and the design-time factory and the other hosts don't have them, so changing it would give them a different model.
- **Email links:** `EmailLinkTokenProvider` is ASP.NET Core Identity's token provider for email confirmation and password reset (`AddDefaultTokenProviders` isn't used, so Data Protection doesn't make these tokens). It stores a SHA-256 hash per `AccountIdentity` and purpose in `email_link_tokens`; the lifetimes are in [security.md](../../../docs/security.md#accounts). Checking a link marks its row deleted, and ASP.NET Core Identity saves that in the same `SaveChanges` as the confirmation or the new password. So a handler must not save anything else in the same scope after an ASP.NET Core Identity call fails; otherwise a rejected password would use up the link.
- **No Data Protection key ring.** Nothing in the Api protects a payload with Data Protection. If a later feature needs it (cookies, antiforgery), it has to bring a persisted, encrypted key ring with it, decided in security.md first.

## Registration and confirmation

`AccountsEndpointModule` maps three endpoints under `/api/v1/auth`:

| Endpoint                   | Body                      | Response | Does                                                                                                                                                                                                 |
| -------------------------- | ------------------------- | -------- | ---------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| `POST register`            | email, password, language | 202      | creates the `Account` and its unconfirmed `AccountIdentity` and requests `EmailConfirmation`; for a taken email, creates nothing and requests `AccountAlreadyExists`. Same empty response either way |
| `POST confirm-email`       | `{ id, token }`           | 204      | confirms the email through ASP.NET Core Identity; the link then stops working. Every failed link (unknown, wrong, expired, used) is 400 ProblemDetails `Accounts.InvalidEmailLink`                   |
| `POST resend-confirmation` | email                     | 202      | requests `EmailConfirmation` for the address without looking it up. Same empty response for any address                                                                                              |

A password that breaks the rules returns 400 ProblemDetails with field errors ([error-handling.md](../error-handling.md)).
