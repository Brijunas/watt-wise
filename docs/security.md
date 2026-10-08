# Watt-Wise — Security

The security decisions for the whole system: accounts, sign-in, sessions, authorization and transport. Facts owned by another doc are linked from here, not repeated. How the pieces are built is in the project docs, e.g. [backend/docs/architecture.md](../backend/docs/architecture.md#authentication).

## Accounts

- **Identity.** ASP.NET Core Identity stores the accounts and hashes the passwords.
- **Sign-in name.** The email address, unique regardless of case.
- **Email confirmation.** A new account can't sign in until its email is confirmed through the emailed link.
- **Passwords.** At least 14 characters, with at least one lowercase letter, one uppercase letter, one digit and one symbol.
- **Lockout.** 5 failed sign-ins in a row lock the account for 15 minutes.
- **No account discovery.** Registering, resending the confirmation and asking for a password reset give the same response whether or not the email has an account. When someone registers with an email that already has one, its owner gets an email saying so instead of a confirmation link. Sign-in returns the "email not confirmed" error only after the password has been checked.
- **Email links.** Confirmation and password-reset links are single-use, and a reset link expires after 1 hour. A link stays valid across a restart of the API.
- **OAuth.** Identity's external-login model stays in place for OAuth sign-in later; nothing uses it in MVP ([product.md](product.md#accounts-and-data)).

## Tokens

- **Access token.** A JWT signed with the key from 1Password, valid for 15 minutes. The response body carries it, the clients keep it in memory only and send it as a Bearer header. It is never written to `localStorage` or `sessionStorage`.
- **Refresh token.** Valid for 7 days and single-use: every refresh returns a new one. It lives in an httpOnly, Secure, `SameSite=Strict` cookie limited to the auth endpoints, so scripts can't read it. The apps and the API share one registrable domain, so the cookie counts as same-site. Only a hash of each token is stored.
- **Reuse.** Presenting a refresh token that was already used ends the session it belongs to.

## Sessions

A session is one sign-in on one device: the chain of refresh tokens that starts at that sign-in.

- **What is kept.** A device label taken from the User-Agent (browser and operating system), the sign-in time and the time of last use. No IP address is stored.
- **What the user can do.** See their sessions, with the current one marked; end any one of them; sign out everywhere, which ends every session including the current one. Sign-out ends the current session.
- **What ends sessions automatically.** A password change or reset ends every session of the account. Losing the `Admin` role ends every session of that account. Deleting the account deletes its sessions ([technical.md](technical.md#cross-cutting), GDPR).
- **When it takes effect.** Ending a session stops its refresh token at once. An access token already issued stays valid until it expires, at most 15 minutes. Making this immediate needs a check on every request, e.g. a cache of ended sessions; it is to be considered later.

## Authorization

- Every account's data is reachable only by that account: consumption objects, their rows, current plans and sessions.
- **Admins.** Admin endpoints live under `/api/v1/admin` and need the Identity `Admin` role. The role is given to and taken from an existing confirmed account with a `WattWise.Cli` command; no endpoint grants it.
- **Hangfire dashboard.** Only local requests until E6, which restricts it to admins ([jobs.md](../backend/docs/jobs.md#dashboard)).

## Transport and limits

- **HTTPS only.** Cloudflare terminates HTTPS and reaches the containers through a tunnel; no port is open on the server ([technical.md](technical.md#hosting-and-operations)).
- **CORS.** Only the frontend and admin origins, and credentials only on the auth endpoints for the refresh cookie. The policy is in [architecture.md](../backend/docs/architecture.md#http-surface); E6 narrows it before the MVP release.
- **Rate and size limits.** Rate limiting on the auth and upload endpoints, upload size limits and security headers come in E6.

## Elsewhere

- **Secrets.** How secrets are kept in 1Password, injected at runtime and kept out of commits: "Secrets and configuration" in [technical.md](technical.md#cross-cutting).
- **Database.** Least-privilege roles, schemas and hardening: [deploy/docs/postgres.md](../deploy/docs/postgres.md).
- **Errors.** Error responses never carry exception details: [backend/docs/error-handling.md](../backend/docs/error-handling.md).
