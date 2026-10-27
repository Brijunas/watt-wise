# Watt-Wise — Security

The security decisions for the whole system: accounts, sign-in, sessions, authorization, transport, deployment, and CI and contributions. Facts owned by another doc are linked from here, not repeated. How the pieces are built is in the project docs, e.g. [backend/docs/features/accounts.md](../backend/docs/features/accounts.md).

## Accounts

- **Account and its sign-in record.** An account is the user: its id, preferred language and creation time, and everything the user owns hangs off it. How the user proves who they are is kept apart from the account, in its `AccountIdentity`: in MVP its email and password, kept by ASP.NET Core Identity; OAuth logins can be added to it later without touching the account. An email is a credential that can change, never the account's key.
- **Sign-in name.** The email address, unique regardless of case.
- **Email normalization.** An email is trimmed, NFKC-normalized and lowercased before it is stored or looked up, and the database keeps it unique.
- **Email confirmation.** A new account can't sign in until its email is confirmed through the emailed link.
- **Passwords.** At least 14 characters, with at least one lowercase letter, one uppercase letter, one digit and one symbol; at most 256 characters. A password is NFKC-normalized before it is hashed, so the same password typed on another keyboard still matches. Hashing is ASP.NET Core Identity's default (PBKDF2-HMAC-SHA512, 100,000 iterations). There is no breached-password list.
- **Lockout.** 5 failed sign-ins in a row lock the account for 15 minutes.
- **No account discovery.** Registering, resending the confirmation and asking for a password reset give the same response whether or not the email has an account. When someone registers with an email that already has one, its owner gets an email saying so instead of a confirmation link. Sign-in returns the "email not confirmed" error only after the password has been checked.
- **Email links.** A confirmation or password-reset link carries a random 256-bit token. Only its SHA-256 hash is stored, so a database dump or backup yields no usable link. An account has at most one live link per kind: sending a new one replaces the old one, and using a link deletes it, so each link works once. A confirmation link expires after 24 hours and a reset link after 1 hour. Links live in the database, so they survive a restart of the API. A confirmation link isn't tied to the address it went to, so changing an account's email (not in MVP) must delete its outstanding confirmation link.
- **OAuth.** ASP.NET Core Identity's external-login model stays in place for OAuth sign-in later; nothing uses it in MVP ([product.md](product.md#accounts-and-data)).

## Tokens

- **Access token.** A JWT signed with the key from 1Password, valid for 15 minutes. The response body carries it, the clients keep it in memory only and send it as a Bearer header. It is never written to `localStorage` or `sessionStorage`, and Redux DevTools, which would show it, is on only in development builds.
- **Refresh token.** Valid for 7 days and single-use: every refresh returns a new one. It lives in an httpOnly, Secure, `SameSite=Strict` cookie limited to the `/api/v1/auth` endpoints, so scripts can't read it. The apps and the API share one registrable domain, so the cookie counts as same-site. Only a hash of each token is stored.
- **Reuse.** Presenting a refresh token that was already used ends the session it belongs to.

## Sessions

A session is one sign-in on one device: the chain of refresh tokens that starts at that sign-in.

- **What is kept.** A device label taken from the User-Agent (browser and operating system), the sign-in time and the time of last use. No IP address is stored.
- **What the user can do.** See their sessions, with the current one marked; end any one of them; sign out everywhere, which ends every session including the current one. Sign-out ends the current session.
- **What ends sessions automatically.** A password change or reset ends every session of the account and deletes its outstanding reset link. Losing the `Admin` role ends every session of that account. Deleting the account deletes its sessions ([technical.md](technical.md#cross-cutting), GDPR).
- **When it takes effect.** Ending a session stops its refresh token at once. An access token already issued stays valid until it expires, at most 15 minutes. Making this immediate needs a check on every request, e.g. a cache of ended sessions; it is to be considered later.

## Authorization

- Every account's data is reachable only by that account: consumption objects, their rows, current plans and sessions.
- **Roles and permissions.** Authorization sits on the account, never on its `AccountIdentity`, so it is the same however the user signed in. An account has roles; a role grants permissions; the account may do what any of its roles grants. Roles and permissions are rows in the database: a new permission or a change to what a role grants needs no code change, although an endpoint can only check a permission name its code knows.
- **Checks.** Endpoints require a permission, never a role name. Sign-in and every refresh put the account's permissions into the access token, so a check reads the token and doesn't query the database. A change to an account's roles or to a role's permissions therefore takes effect within one access-token lifetime (15 minutes); revoking a role also ends the account's sessions.
- **Admins.** Admin endpoints live under `/api/v1/admin` and need the `admin.access` permission, which the `Admin` role grants. The role is given to and taken from an existing confirmed account with a `WattWise.Cli` command; no endpoint grants it.
- **Hangfire dashboard.** Never reachable from the internet. It accepts only requests from the machine it runs on, no tunnel route leads to the Jobs host, and from E6 it is an internal UI on saturn's loopback address like Grafana ([Deployment](#deployment), [jobs.md](../backend/docs/jobs.md#dashboard)). It has no sign-in of its own, so the `Admin` role doesn't apply to it. Browser access for admins is reconsidered if the system ever gets its own OpenID Connect sign-in.

## Transport and limits

- **HTTPS only.** Cloudflare terminates HTTPS and reaches the containers through a tunnel; no port is open to the network on the server ([technical.md](technical.md#hosting-and-operations)).
- **CORS.** Only the frontend and admin origins, and credentials only on the `/api/v1/auth` endpoints for the refresh cookie. The policy is in [architecture.md](../backend/docs/architecture.md#http-surface); E6 narrows it before the MVP release.
- **Origin check (S4.4).** An unsafe request (POST, PUT, PATCH, DELETE) under `/api/v1` whose `Origin` header is not one of the CORS origins gets 403 ProblemDetails before it reaches an endpoint. A `SameSite=Strict` cookie doesn't stop this: another subdomain of the same site is same-site and still sends it, and CORS only stops the browser from reading the answer, not the server from acting on it. .NET 11's built-in cross-origin check doesn't cover it either, because it acts only on form and antiforgery endpoints (checked on 11.0 RC 2, October 2026). Requests without an `Origin` (non-browser clients) and same-origin ones pass.
- **Client address.** The Api uses the connection's own address, except on requests that come from the tunnel container: there it takes the address from Cloudflare's `CF-Connecting-IP` header. It is used for rate limiting, held in memory only and never stored or logged.
- **Rate limits (E6).** The `/api/v1/auth` endpoints share one counter: 10 requests a minute per IP address across all of them together. The endpoints that send email (register, resend confirmation, forgot password) share a second one: 5 requests an hour per IP address across the three. Uploads: 20 an hour per account. A request over a limit gets 429 ProblemDetails with `Retry-After`.
- **Size limits (E6).** An upload body is at most 5 MB; a larger one gets 413 ProblemDetails.
- **Security headers (E6).** Every response of the Api, the Jobs host and both apps carries `Strict-Transport-Security`, `X-Content-Type-Options: nosniff`, `Referrer-Policy: strict-origin-when-cross-origin` and a `Content-Security-Policy` with `frame-ancestors 'none'`. The Api's policy is `default-src 'none'`; the Scalar page outside Production and the Hangfire dashboard get a looser one, only as far as they need. The apps' policy allows scripts only from their own origin plus the hashes of their inline scripts, and connections only to their own origin and the API. The exact policies and the `Strict-Transport-Security` value are recorded here by S6.2.
- **Inline script.** Each app's `index.html` carries one inline script, the one that applies the stored appearance before first paint ([frontend/docs/architecture.md](../frontend/docs/architecture.md#appearance)). Its content is fixed at build time, so its hash is the one the apps' policy lists.

## Deployment

How releases reach the server and how its secrets are kept. The repository is public, so anyone can fork it and open a pull request; nothing a pull request runs may reach the server or a deploy credential. The deploy flow itself is under "Hosting and operations" in [technical.md](technical.md#hosting-and-operations).

- **Nothing connects in.** saturn opens no inbound port and has no SSH hostname. GitHub holds no credential for it. saturn pulls: an agent on it polls the registry and deploys what it can verify.
- **Signed images.** The publishing workflow signs every image with keyless cosign (GitHub's OIDC identity). Before it deploys anything, the agent runs `cosign verify` against this repository's publishing workflow and the branch the environment accepts: `main` for Staging and Production, `main` or `dev` for Testing. It deploys by digest, never by tag. A fork, a pull request or a workflow on another branch can't produce a signature it accepts.
- **Deploy tags.** A deploy or a swap only moves the `testing`, `staging` or `production` tag in the registry. The jobs that move them run in GitHub environments limited to their branch: `testing` to `dev`, `staging` and `production` to `main`. Production changes only through the swap, started by a person; no push moves its tag. Moving a tag can at worst point an environment at an older signed image.
- **Secrets on the server.** One 1Password service account per deployed environment, read-only on that environment's vault and nothing else. Its token sits on saturn in a file only root can read, and nowhere else. The agent resolves the environment's `compose.env` with it at deploy time, so each container gets only its own settings: the Api never sees the Jobs or Cli database passwords, and so on. The agent reports each deploy to a GitHub deployment status with a fine-grained token limited to deployments on this repository, kept the same way.
- **Images.** They hold no settings or secrets, so they are published publicly like the repository. The app containers run as a non-root user.
- **Internal UIs.** Grafana (the shared LGTM), Testing's Mailpit and, from E6, the Hangfire dashboard publish only on saturn's loopback address; no tunnel route leads to them. PostgreSQL publishes no port at all ([postgres.md](../deploy/docs/postgres.md#hardening)).
- **What the repo says about saturn.** Its name, that it runs Docker and the Compose stacks, and nothing more: no hostname, address, location, network layout or host configuration, and nothing about where backups go once they leave the server: the off-server copy, its retention and the dump folder's path. What the stacks themselves do, such as the daily database dump, is in the repo. Those live in a private runbook, a Secure Note in 1Password. Outgoing email is checked for headers that carry the server's address ([epics.md](epics.md), S5.9).

## CI and contributions

- **Hosted runners only.** Every workflow runs on GitHub-hosted runners. No self-hosted runner is ever attached to this repository.
- **Fork pull requests.** Workflows from every external contributor wait for the maintainer's approval (the repository's strictest setting). No workflow uses `pull_request_target`, which runs with the base repository's secrets and skips that approval. The pull request workflow gets no secrets.
- **Least privilege.** The default `GITHUB_TOKEN` is read-only, and each job grants itself only the `permissions` it needs; only the signing job gets `id-token: write`. GitHub Actions may not create or approve pull requests.
- **Pinned actions.** Every action is pinned to a full commit SHA, enforced by the repository's SHA-pinning policy and kept current by Renovate. The pull request workflow lints the workflows with zizmor.
- **Signed commits.** `main` accepts only signed commits. Pull requests are squash-merged, so GitHub signs the commit that lands.
- **External pull requests.** The maintainer reads the diff before approving its workflows, above all `.github/`, `*.csproj`, `Directory.Build.*`, `package.json` scripts and the hook configuration. If it has to run, it runs in a Codespace or a throwaway VM, never on a machine that holds SSH keys, a 1Password session or `.env` files.

## Elsewhere

- **Secrets.** How secrets are kept in 1Password, injected at runtime and kept out of commits: "Secrets and configuration" in [technical.md](technical.md#cross-cutting).
- **Backups.** Database dumps hold email addresses and password hashes. Each dump is encrypted with a key from 1Password before it is written, and its folder is readable only by the dump service's user and root. A deleted account's data stays in the off-server backup history until that history's retention period ends; the period is in the private runbook ([postgres.md](../deploy/docs/postgres.md)).
- **Database.** Least-privilege roles, schemas and hardening: [deploy/docs/postgres.md](../deploy/docs/postgres.md).
- **Errors.** Error responses never carry exception details: [backend/docs/error-handling.md](../backend/docs/error-handling.md).
