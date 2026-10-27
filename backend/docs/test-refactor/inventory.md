# Backend test refactor: inventory

Phase 1 of the backend test refactor: what the Accounts feature does, what the tests check today, and the baseline the rewrite is measured against. Surveyed on `4ab46ca` (2026-10-27), read-only. Every section separates **Facts** (checked, with file paths) from **Open questions**, which Phase 2 answers in [decisions.md](decisions.md). The verdict per test file and the support code are in [inventory-tests.md](inventory-tests.md); the baseline numbers and every surviving mutant are in [baseline.md](baseline.md).

Paths starting with `src/` or `tests/` are under `backend/`. "security.md" is the root `docs/security.md`.

## 1. Accounts behaviors

Criticality: **high**, a regression breaks sign-in, leaks a credential or lets one account act as another; **medium**, it corrupts or loses account data or breaks a rule a doc states; **low**, a guard with no user-visible effect today. The owner layer follows testing.md: Domain is unit-tested; Infrastructure has integration tests only.

Facts:

| #   | Behavior                                                                                                                                | Crit.  | Owner          | Expected value from                                                                                                          | Tested today                                         |
| --- | --------------------------------------------------------------------------------------------------------------------------------------- | ------ | -------------- | ---------------------------------------------------------------------------------------------------------------------------- | ---------------------------------------------------- |
| B1  | `Account.Create` gives a version 7 id, keeps the language and the time, starts with no roles                                            | medium | Domain         | domain-model.md (v7 ids), accounts.md "Model"                                                                                | yes                                                  |
| B2  | `Account.AddRole` is idempotent and rejects `Guid.Empty`                                                                                | medium | Domain         | idempotence: comment `src/WattWise.Domain/Accounts/Account.cs:41`; `Guid.Empty`: code only (`:44`)                           | idempotence only; `Guid.Empty` is a surviving mutant |
| B3  | `Role.Create` needs a non-blank name; `Role.Grant` is idempotent and rejects `Guid.Empty`                                               | medium | Domain         | code only (`Role.cs:28,36`), comment `:33`                                                                                   | `Guid.Empty` is a surviving mutant                   |
| B4  | `Permission.Create` needs a non-blank name and a non-null description                                                                   | low    | Domain         | code only (`Permission.cs:30-31`)                                                                                            | the description check is a surviving mutant          |
| B5  | An email is trimmed, NFKC-normalized and lowercased in `Email`, `UserName` and both normalized columns                                  | high   | Infrastructure | security.md "Email normalization"                                                                                            | trim and lowercase; NFKC never                       |
| B6  | An email is unique regardless of case: `UserManager` returns `DuplicateEmail`, and `ix_identity_users_normalized_email` closes the race | high   | Infrastructure | security.md "Sign-in name", "Email normalization"                                                                            | yes                                                  |
| B7  | One `AccountIdentity` per account (unique `account_id`)                                                                                 | medium | Infrastructure | accounts.md "Model"                                                                                                          | yes; constraint name only by substring               |
| B8  | Passwords: at least 14 characters with a lowercase, an uppercase, a digit and a symbol; at most 256                                     | high   | Infrastructure | security.md "Passwords"                                                                                                      | yes                                                  |
| B9  | Those limits count the NFKC form: 12 typed characters that normalize to 14 pass, 256 that normalize to 258 fail                         | medium | Infrastructure | **code only**: `src/WattWise.Infrastructure/Accounts/NormalizingPasswordValidator.cs:17`, `PasswordMaxLengthValidator.cs:14` | yes (characterization)                               |
| B10 | A password is NFKC-normalized before hashing: set in NFC, it verifies in NFD                                                            | high   | Infrastructure | security.md "Passwords"                                                                                                      | yes, on the in-memory entity, never reloaded         |
| B11 | Hashing is PBKDF2-HMAC-SHA512, 100,000 iterations                                                                                       | low    | Infrastructure | security.md "Passwords" (ASP.NET Core Identity's default)                                                                    | no                                                   |
| B12 | 5 failed attempts in a row lock the `AccountIdentity` for 15 minutes; new identities can be locked                                      | high   | Infrastructure | security.md "Lockout"                                                                                                        | yes, in one scope with a wall-clock window           |
| B13 | An unconfirmed email can't sign in (`RequireConfirmedEmail`)                                                                            | high   | Api (S4.2)     | security.md "Email confirmation"                                                                                             | no; no sign-in endpoint exists yet                   |
| B14 | A link token is 32 random bytes in Base64Url; only its SHA-256 hash is stored                                                           | high   | Infrastructure | security.md "Email links"                                                                                                    | yes                                                  |
| B15 | One live link per `AccountIdentity` and kind: a new one replaces the old; two requested at once leave one that works                    | high   | Infrastructure | security.md "Email links"                                                                                                    | yes                                                  |
| B16 | A link works once; of two uses at once exactly one succeeds                                                                             | high   | Infrastructure | security.md "Email links"                                                                                                    | yes                                                  |
| B17 | Confirmation links last 24 h, reset links 1 h; at the expiry instant itself the link no longer works                                    | high   | Infrastructure | lifetimes: security.md; the instant: **code only** (`EmailLinkTokenProvider.cs:63`, `>=`)                                    | 1 min / 1 s either side; never the instant           |
| B18 | A link only works for its own `AccountIdentity` and its own kind                                                                        | high   | Infrastructure | security.md "Email links"                                                                                                    | yes                                                  |
| B19 | Only the canonical 43-character Base64Url form is accepted; malformed input fails without throwing                                      | medium | Infrastructure | **code only** (`EmailLinkTokenProvider.cs:129-142`)                                                                          | yes, six inputs looped in one Fact                   |
| B20 | A rejected new password doesn't use up the reset link                                                                                   | high   | Infrastructure | accounts.md "Email links"                                                                                                    | yes                                                  |
| B21 | Links survive an Api restart (they live in the database)                                                                                | medium | Infrastructure | security.md "Email links"                                                                                                    | yes, but passes trivially (inventory-tests.md)       |
| B22 | A link of any other purpose throws `NotSupportedException`                                                                              | low    | Infrastructure | code only (`EmailLinkTokenProvider.cs:125`)                                                                                  | no                                                   |
| B23 | Deleting an account deletes its `AccountIdentity`, role links and email links; roles stay                                               | high   | Infrastructure | security.md "Sessions" (deleting the account), technical.md (GDPR); cascades in `Persistence/Configurations/`                | in two halves; the identity row isn't pre-checked    |
| B24 | Deleting a role removes its links, not accounts or permissions; deleting a permission removes it from every role                        | medium | Infrastructure | **code only** (`RoleConfiguration.cs:18-21`, `AccountRoleConfiguration.cs:14-17`, `RolePermissionConfiguration.cs:14-17`)    | yes; the role case has no precondition               |
| B25 | Role names and permission names are unique                                                                                              | medium | Infrastructure | comments `Role.cs:21`, `Permission.cs:23`                                                                                    | yes                                                  |
| B26 | The database holds an account's role once (primary key on `account_roles`)                                                              | medium | Infrastructure | code only (`AccountRoleConfiguration.cs:13`)                                                                                 | the test is vacuous (section 8)                      |
| B27 | The language is stored as `lt` / `en`; other stored text fails to load                                                                  | low    | Infrastructure | **code only** (`AccountConfiguration.cs:32-49`)                                                                              | stored form yes; unknown text no                     |
| B28 | `CreatedAt` round-trips at microsecond precision                                                                                        | medium | Infrastructure | testing.md "PostgreSQL integration tests"                                                                                    | yes, with a 1000 ns tolerance                        |
| B29 | Schema `app` has exactly the nine account tables, none of ASP.NET Core Identity's role or claim tables                                  | medium | Infrastructure | accounts.md "Model"                                                                                                          | yes                                                  |

Open questions (answered in [decisions.md](decisions.md)):

- **Q1.** Which flows must never break? Proposal: every high row (B5, B6, B8, B10, B12–B18, B20, B23). Answered: **D1**.
- **Q2.** The **code only** rows B9, B17 (the instant), B19, B24 and B27 need characterization approval. Answered: **D8**.
- **Q3.** B13: leave until a sign-in endpoint exists (S4.2), or pin the option now? Proposal: leave it; an option-value test catches nothing a sign-in test won't. Answered: **D6**.
- **Q4.** B11: proposal, no test. It is the library default, and a test would have to decode ASP.NET Core Identity's hash format. Answered: **D6**.

## 2. Existing tests

Facts: 39 test files, 120 test methods (105 Facts, 15 Theories with 52 cases), 157 test runs. Proposed verdicts, file by file in [inventory-tests.md](inventory-tests.md): keep 6, rewrite 25, merge 1, delete 7. The rules broken most often:

- one behavior per test: tests that assert two behaviors (e.g. `EmailLinkTokenTests.cs:25,42,160,240`, `ErrorMappingTests.cs:75`, `ParentlessDatabaseSpanFilterTests.cs:14`), branch on a Theory argument (`EmailLinkTokenTests.cs:87,107`) or loop cases in one Fact (`:183`);
- read back through the context that wrote: `AccountModelTests.cs:45,87`, `EmailRuleTests.cs:39`, `LockoutTests.cs:14`, `PasswordRuleTests.cs:83`, `RolesAndPermissionsTests.cs:17,82,106`;
- weak assertions: "doesn't throw" only (`DatabaseMigratorTests.cs:25`), non-blank only (both About tests, `NoOpJobTests.cs:41`), `NotEmpty` messages (`MediatorPipelineTests.cs:29`), a tautology (`NoOpJobTests.cs:19`, `OtlpExportTests.cs:14`), any `OptionsValidationException` (`CorsValidationTests.cs:7`);
- expected value from production code: `DatabaseRoundTripTests.cs:24` (EF's own migration list), `RoleAccessTests.cs:141` (`DatabaseSchemas.Migrations`);
- five `LoadsWithExpectedName` tests, one per project, fail only if an assembly is renamed.

No test uses a mocking library, an assertion library, EF InMemory, SQLite or `EnsureCreated`; every async call that accepts a token gets `TestContext.Current.CancellationToken`.

Open question (answered in [decisions.md](decisions.md)): **Q5.** Approve the verdicts in inventory-tests.md (Phase 2 asks per project). Answered: **D4**.

## 3. Duplication

Facts, with the proposed owner (the lowest layer that reaches the behavior):

| Behavior                                        | Tested in                                                                                    | Proposed owner                                                                                  |
| ----------------------------------------------- | -------------------------------------------------------------------------------------------- | ----------------------------------------------------------------------------------------------- |
| About returns the name and a version            | `GetAboutQueryHandlerTests.cs:8`, `AboutEndpointTests.cs:18`                                 | Application; the Api test keeps route, 200 and JSON shape                                       |
| An unhandled exception is logged once as Error  | `ErrorMappingTests.cs:75`, `ObservabilityTests.cs:51`                                        | ObservabilityTests; ErrorMappingTests keeps the response                                        |
| A parentless Npgsql span isn't recorded         | `ParentlessDatabaseSpanFilterTests.cs:14`, `BackgroundSpanTests.cs:16`                       | the filter test; the Jobs test only proves the Jobs host installs it                            |
| A failing health check doesn't leak its message | `HealthResponseWriterTests.cs:14`, `HealthEndpointTests.cs:26`                               | the writer test; the endpoint test keeps 503, content type and status                           |
| A connection uses the expected role             | `DatabaseRoundTripTests.cs:12`, `JobsRoleTests.cs:10`, `RoleAccessTests.cs:37-139`           | RoleAccessTests: a wrong role fails its privilege cases                                         |
| Tables belong to `owner`                        | `HangfireSchemaTests.cs:13`, `RoleAccessTests.cs:141`                                        | one ownership Theory in RoleAccessTests                                                         |
| Every migration is applied                      | `DatabaseRoundTripTests.cs:24`, `DatabaseMigratorTests.cs:25`, `InitialMigrationTests.cs:18` | InitialMigrationTests (exact tables) and DatabaseMigratorTests (rerun leaves history unchanged) |
| A link's second use fails                       | `EmailLinkTokenTests.cs:25,42`                                                               | one Theory over both kinds                                                                      |
| `AddRole` / `Grant` are idempotent              | `AccountTests.cs:24`, `RoleTests.cs:32`, `RolesAndPermissionsTests.cs:17`                    | Domain; the Infrastructure test is replaced by the B26 race                                     |
| Logging → Validation behavior order             | `AboutEndpointTests.cs:31` (reads `Program.cs`), `PipelineHost.cs:33` (a copy)               | AboutEndpointTests                                                                              |

## 4. Gaps

Facts: high and medium behaviors no test checks.

| Behavior                                                                                  | Crit.  | Layer          |
| ----------------------------------------------------------------------------------------- | ------ | -------------- |
| B5 NFKC in emails (e.g. a full-width `＠` or `ﬁ`)                                         | high   | Infrastructure |
| B17 a link at its exact expiry instant                                                    | high   | Infrastructure |
| B12 the lockout persisted: read back in a new scope                                       | high   | Infrastructure |
| B23 one account deletion removing identity, role links and email links                    | high   | Infrastructure |
| B26 two contexts adding the same role at once: one wins, the other gets 23505             | medium | Infrastructure |
| B2–B4 the `Guid.Empty` and null-description guards (surviving mutants)                    | medium | Domain         |
| `DomainException` keeps its code and inner exception (no coverage)                        | medium | Domain         |
| CORS methods, headers, 10-minute max age, no credentials (architecture.md "HTTP surface") | medium | Api            |
| A host stops on missing or invalid `Database` settings (development.md "Troubleshooting") | medium | Api, Jobs      |

`WattWise.Cli` has no test project.

Open question (answered in [decisions.md](decisions.md)): **Q6.** Which gaps join the rewrite? Proposal: all rows; the Cli stays out (production-side project, not in this refactor's scope). Answered: **D5**.

## 5. Platform tests

Facts: verdicts per file in [inventory-tests.md](inventory-tests.md).

- **RoleAccessTests** (6 methods, 20 runs): the role matrix from deploy/docs/postgres.md; sound. Fix the production constant at `:141`. `reader` reading `migrations` follows `bootstrap.sql:63-64`, which postgres.md contradicts (section 8). The `backup` role is never tested.
- **ErrorMappingTests** (11, 12 runs): covers every row of error-handling.md's contract table. `:75` asserts three behaviors; `:138` never drives the RFC-type branch of its helper.
- **ObservabilityTests** (2): sound correlation checks; `:14` asserts log shape, correlation and span count in one test. It renders compact JSON with a new formatter instead of capturing the host's output.
- **ObservabilityValidationTests** (1, 5 runs): sound; checks the settings key.
- **OtlpExportTests** (1): proves some exporter connects, not that traces export; ends on a tautology.
- **Pipeline** (`MediatorPipelineTests`, `RequestContractTests`): real generated mediator, sound setup; weak message assertions; `RequestContractTests` has no negative case.
- **Migration tests** (`DatabaseMigratorTests`, `InitialMigrationTests`, `DatabaseRoundTripTests`, `HangfireSchemaTests`): overlap (section 3); one no-throw-only test.

Open question (answered in [decisions.md](decisions.md)): **Q7.** Are platform tests in scope for rewriting, or only for review? Proposal: in scope, same rules, each in its project's batch. Answered: **D2**.

## 6. Test support code

Facts, details in [inventory-tests.md](inventory-tests.md#support-code):

- **Sound:** `PostgresContainerFixture` and `DatabaseFixture` (real bootstrap and migrations, a clone per class), `TestHostFactory`, `Poll`, `SaveChangesBarrier`, `CommandBarrier`, `ProblemDetailsAssertions`, `LogEventCollector` / `SpanCollector` (filtered by trace id as testing.md says).
- **Doesn't match testing.md or is fragile:** `AccountsTestSupport.CreateAccountIdentityAsync` writes in the caller's scope, inviting same-context reads; `UniqueViolation`, `CountAsync` and `Token` are copied into several classes; `ProblemDetailsAssertions` checks `traceId` only for non-empty and ignores `title` / `detail`; `ErrorsApiFixture` gives each of its 5 classes its own host and database; `TestDatabase.DropAsync` clears every Npgsql pool in the process (`TestDatabase.cs:30`); the container start retries on any exception (`PostgresContainerFixture.cs:68`); `PipelineHost.cs:33` copies the host's behavior list by hand.
- `CsCheck` and `NodaTime.Testing` are referenced by Domain.Tests and Application.Tests but unused there.

Open question (answered in [decisions.md](decisions.md)): **Q8.** May `WattWise.Testing` and the per-project support classes change? Proposal: yes, in a support batch before the first batch that needs it. Answered: **D3**.

## 7. Baseline

Facts, full detail in [baseline.md](baseline.md):

| Project                         | Tests | Run time (MTP, mean of 2) | Mutation score        |
| ------------------------------- | ----- | ------------------------- | --------------------- |
| Domain.Tests                    | 14    | 0.41 s                    | 69.23 % (Domain)      |
| Application.Tests               | 23    | 0.50 s                    | 65.62 % (Application) |
| Infrastructure.IntegrationTests | 72    | 6.71 s                    | n/a                   |
| Api.IntegrationTests            | 41    | 6.08 s                    | n/a                   |
| Jobs.IntegrationTests           | 7     | 5.55 s                    | n/a                   |

Of 8 Domain survivors, 5 can be killed by a test; 3 are `string.Empty` initializers only EF's constructor sees. Of 11 Application survivors, 1 can be killed with a characterization value, 3 are exception or description strings, and 7 can't be reached without a production change or are equivalent. Reports: `backend/StrykerOutput/Baseline/{Domain,Application}/reports/` (git-ignored copies).

Open question (answered in [decisions.md](decisions.md)): **Q9.** Target per project and the `break` threshold. Proposal: Domain ≥ 88 % (every killable survivor: 23 of 26), Application ≥ 68 %; `break` stays 60 until the last batch, then rises to the lowest final score rounded down. Answered: **D19, D17, D18**.

## 8. Suspected bugs

Facts, none fixed. No production bug is confirmed; these are a vacuous test and doc conflicts.

1. `tests/WattWise.Infrastructure.IntegrationTests/Accounts/RolesAndPermissionsTests.cs:17` is vacuous: the loaded account already carries the role (`AutoInclude`, `src/WattWise.Infrastructure/Persistence/Configurations/AccountConfiguration.cs:28`), so `AddRole` hits the domain no-op (`Account.cs:46`) and nothing is saved. It can't catch a missing primary key.
2. `deploy/docs/postgres.md:37` says nobody but `owner` reads the `migrations` schema; `deploy/postgres/bootstrap.sql:63-64` gives `reader` `pg_read_all_data`, which postgres.md's own reader row also says. `RoleAccessTests.cs:62` follows the SQL.
3. `tests/WattWise.Jobs.IntegrationTests/NoOpJobTests.cs:67`: the comment expects E4 to add admin access to the dashboard; security.md "Authorization" says it never gets one. Test code, not production.
4. Password length is counted in UTF-16 code units after NFKC (`PasswordMaxLengthValidator.cs:14`, ASP.NET Core Identity's `RequiredLength`): a password of 129 emoji is 258 "characters" and is rejected. security.md says "characters" without defining them.
5. `src/WattWise.Infrastructure/Accounts/TextNormalization.cs:14-17` returns text that isn't valid Unicode unchanged, so a password with a lone surrogate is hashed without normalization. No doc covers it.

Open questions (answered in [decisions.md](decisions.md)): **Q10.** For 2, 4 and 5: keep current behavior in the tests, or leave the case out and note it for a fix? Item 2 needs a deploy doc change, which this refactor's scope (backend tests only) excludes. Answered: **D9, D10, D11, D12**.

## 9. Policy issues

Facts: places where testing.md is unclear, contradicts itself or works against the lean goal.

- **P1. Unit tests outside Domain and Application.** testing.md says Infrastructure, Api and Jobs have no or few unit tests, and also "if a scenario can be tested with a unit test, test it with a unit test". Pure code sits in those layers (`TextNormalization`, the token decoding, `ParentlessDatabaseSpanFilter`, `HealthResponseWriter`, `ProblemDetailsCustomization`, both settings validators); two such tests already live in integration projects.
- **P2. Mutation testing misses most of Accounts.** Stryker runs on Domain and Application only; B5–B29 live in Infrastructure, so the score can't measure most of this rewrite.
- **P3. "One behavior per test" is undefined.** Whether several asserts on one outcome (a response's status, code and body; one cascade's three tables) count as one behavior.
- **P4. "One happy path and at least one sad path"** for argument guards with no caller yet (B2–B4) and for branches only a production change can reach (the About version fallback).
- **P5. String mutants.** Exception and description texts survive (`Result.cs:27,31`, `Error.cs:18`); pinning them only adds characterization values.
- **P6. Time.** testing.md fakes `IClock` only; ASP.NET Core Identity's lockout reads its own clock, so B12 has a wall-clock window. Whether its `TimeProvider` can be replaced from DI is not yet verified.
- **P7. The PostgreSQL list** asks for 409/404/422 mappings of unique and foreign-key violations, but `ErrorType` has no conflict category and no use case maps them yet.
- **P8. Duplicated rules.** The assertion rules and the PostgreSQL list are in testing.md and in both agents. Proposal: keep them only in testing.md; the agents link to its sections.
- **P9. Smoke tests** such as `LoadsWithExpectedName` aren't covered by any rule.

Open question (answered in [decisions.md](decisions.md)): **Q11.** Which policy changes to make? Each needs a concrete testing.md change, proposed in Phase 2. Answered: **D15, D16**.

## Open questions

None. Every question above is answered in [decisions.md](decisions.md), as are Q12, the batch order (D7), and Q13, the reviewer's model (D20).
