# Accounts

The account is the user: its id, preferred language and creation time, and everything the user owns hangs off it. This doc is how the feature is built: the model, ASP.NET Core Identity and, from S4.2 on, the sign-in flows. Each E4 story adds its section here. What the feature enforces (passwords, lockout, tokens, sessions, permissions) is decided in the root [security.md](../../../docs/security.md); the token flow seen by clients is in the root [technical.md](../../../docs/technical.md#backend).

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

| Project                                       | Where                         | What                                                                                                                                                                                                                   |
| --------------------------------------------- | ----------------------------- | ---------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| Domain                                        | `Accounts/`                   | `Account`, `AccountRole`, `Role`, `RolePermission`, `Permission`                                                                                                                                                       |
| Application                                   | `Accounts/`                   | Use cases and ports, from S4.3                                                                                                                                                                                         |
| Infrastructure                                | `Accounts/`                   | The ASP.NET Core Identity adapter: `AccountIdentity`, `EmailLinkToken` and `EmailLinkTokenProvider`, `EmailLookupNormalizer`, `NormalizingPasswordHasher`, the password validators                                     |
| Infrastructure                                | `Persistence/Configurations/` | The EF mappings (`AccountConfiguration`, `AccountIdentityConfiguration`, …), in the folder every feature shares                                                                                                        |
| Api                                           | `Endpoints/`                  | `AccountsEndpointModule`, from S4.3. It maps the `/api/v1/auth` route group; the public name stays `auth`, like the shared i18n namespace ([frontend/docs/structure.md](../../../frontend/docs/structure.md#features)) |
| Domain.Tests, Infrastructure.IntegrationTests | `Accounts/`                   | The feature's tests. `AccountsTestSupport` builds services for the `api` role, with an optional `FakeClock`, and makes accounts, `AccountIdentity` records and unique emails                                           |

## Model

ASP.NET Core Identity in Infrastructure, on `AppDbContext`, handles the account's `AccountIdentity`: password hashing, lockout, email tokens and (later) external OAuth logins.

- **Three groups of tables** in the `app` schema:
  - **Account:** `accounts` (Domain `Accounts/Account`).
  - **Authentication:** `identity_users` (one per account, `account_id` unique), `identity_user_logins` and `identity_user_tokens` (kept for OAuth, unused), `email_link_tokens` (one per `AccountIdentity` and purpose).
  - **Authorization:** `roles`, `account_roles`, `permissions`, `role_permissions` (Domain `Accounts/Role`, `Permission`, and the link entities `AccountRole`, `RolePermission`).

  `AppDbContext` derives from `IdentityUserContext`, so ASP.NET Core Identity's own role and claim tables are not in the model.

  `account_roles` and `role_permissions` hold ids: `Account` keeps its roles as `AccountRole` links and exposes `RoleIds`, `Role` keeps `RolePermission` links and exposes `PermissionIds`; neither has a navigation to the other aggregate ([domain-model.md](../domain-model.md#aggregates)). What an account's roles grant is read by a query (S4.4), never by loading roles.

## ASP.NET Core Identity

- **Registration.** `AddInfrastructure()` registers ASP.NET Core Identity (its private `AddIdentity()`), its options and the email-link token provider, so every host has it with the same rules; only the Api uses it today.
- **Store options.** `IdentityOptions.Stores` stays at its defaults: ASP.NET Core Identity reads it from the app's services while building the model, and the design-time factory and the other hosts don't have them, so changing it would give them a different model.
- **Email links:** `EmailLinkTokenProvider` is ASP.NET Core Identity's token provider for email confirmation and password reset (`AddDefaultTokenProviders` isn't used, so Data Protection doesn't make these tokens). It stores a SHA-256 hash per `AccountIdentity` and purpose in `email_link_tokens`; the lifetimes are in [security.md](../../../docs/security.md#accounts). Checking a link marks its row deleted, and ASP.NET Core Identity saves that in the same `SaveChanges` as the confirmation or the new password. So a handler must not save anything else in the same scope after an ASP.NET Core Identity call fails; otherwise a rejected password would use up the link.
- **No Data Protection key ring.** Nothing in the Api protects a payload with Data Protection. If a later feature needs it (cookies, antiforgery), it has to bring a persisted, encrypted key ring with it, decided in security.md first.
