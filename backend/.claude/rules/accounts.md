---
paths:
  - 'src/**/Accounts/**/*.cs'
  - 'tests/**/Accounts/**/*.cs'
---

# Accounts

- The feature's folder is `Accounts/` in every project; in a shared folder (`Persistence/Configurations/`, `Endpoints/`) its files carry the word. The route group stays `/api/v1/auth`.
- `Account` (Domain) is the user and the owner of their data; `AccountIdentity` (Infrastructure) is its email-and-password sign-in record, linked by `AccountId`. No ASP.NET Core Identity type goes in Domain.
- Roles and permissions are referenced by id; what an account's roles grant is read by a query, never by loading roles.
- After a failed ASP.NET Core Identity call, a handler saves nothing else in that scope: a checked email link is already marked deleted.
- Password, lockout, token and session rules are decided in the root `docs/security.md`; change them there first. Words follow the Vocabulary in the feature doc.

The full design is in [accounts.md](../../docs/features/accounts.md).
