# Backend test refactor: decisions

Answers to the open questions in the [inventory](inventory.md), from the Phase 2 interview on 2026-10-27. Each decision names the question it answers. Batches follow these; testing.md is changed to match in the final batch (D15).

## Scope and priorities

- **D1 (Q1, critical flows).** Every high row of inventory section 1 must never break: B5, B6, B8, B10, B12–B18, B20, B23. Their tests are never dropped and get the strictest review.
- **D2 (Q7, platform tests).** In scope for rewriting, held to the same rules, each in its project's batch.
- **D3 (Q8, support code).** `WattWise.Testing` and the per-project support classes may change, in a support batch before the first batch that needs them.
- **D4 (Q5, verdicts).** The per-file verdicts in [inventory-tests.md](inventory-tests.md) are approved as listed: keep 6, rewrite 25, merge 1, delete 7. Each batch still maps every old test to its replacement or "dropped: <reason>".
- **D5 (Q6, gaps).** These gaps join the rewrite: the Accounts gaps (email NFKC, a link at its expiry instant, lockout read back in a new scope, one account-deletion cascade, the `account_roles` race, the Domain guards and `DomainException`), the CORS headers from architecture.md, and the hosts stopping on missing or invalid `Database` settings. `WattWise.Cli` gets no tests in this refactor.
- **D6 (Q3, Q4).** B13 (`RequireConfirmedEmail`) waits for the sign-in endpoint (S4.2). B11 (the hashing algorithm) gets no test.
- **D7 (Q12, batch order).** Support → Domain → Application → Infrastructure → Api → Jobs → final.

## Expected values and suspected bugs

- **D8 (Q2, characterization).** Approved as characterization, each marked with its `file:line`: B9 (password limits count the NFKC form), B17 (at the expiry instant the link no longer works), B19 (only the canonical token form is accepted), B24 (role and permission deletion cascades), B27 (`lt` / `en`, other text fails to load), and the About version (D13).
- **D9 (Q10, password length).** Not a bug: a "character" is a UTF-16 code unit of the NFKC form, so one emoji counts as 2. Tests pin today's behavior.
- **D10 (Q10, lone surrogates).** A bug: a password that isn't valid Unicode should be rejected (today `TextNormalization.cs:14-17` hashes it unnormalized). It needs a production change, outside this refactor; no test for it until then.
- **D11 (Q10, `reader` and `migrations`).** Investigated: not a bug in the SQL. `backup` (S2.3, for `pg_dump`) and `reader` (#48, "read every table") both get `pg_read_all_data` on purpose (`deploy/postgres/bootstrap.sql:63-64`, postgres.md's role table). The schema table's "Nobody: only `owner`" (`deploy/docs/postgres.md:37`, from S2.12) describes explicit grants and is wrong about who can read. Tests follow the SQL.
- **D12 (out-of-scope fixes).** Noted only, no story yet: security.md should define "characters" as D9 does; postgres.md:37 should say `backup` and `reader` read the schema through `pg_read_all_data`; the lone-surrogate rejection (D10); observability.md "OTLP endpoint" says "no path" but `http://host:4317/` is accepted (`ObservabilitySettings.cs:24`, pinned as characterization in batch 3), so either the doc says "no path other than `/`" or the code rejects it.

## Rules for the rewrite

- **D13 (P5, string mutants).** Pin the About version as characterization (API output; the SDK's informational version, never `0.0.0`). Exception messages (`Result.cs:27,31`) and the unused validation description (`Error.cs:18`) stay accepted survivors.
- **D14 (P4, sad paths).** Every branch a test can reach gets a happy and a sad path, argument guards included. Branches only a production change can reach are listed as unreachable in the batch.
- **D15 (Q11, P1–P9, testing.md).** Approved, applied in the final batch with wording reviewed in plan.md:
  1. New unit projects `WattWise.Infrastructure.Tests` and `WattWise.Api.Tests` for code that needs no database or host (P1, D16).
  2. One behavior per test is one logical assert: one `Assert` call, where a helper that checks one contract (such as `ProblemDetailsAssertions`) or one `Assert.Equal` on a tuple or record of the outcome counts as one; precondition checks in Arrange are allowed. Variants go in a `[Theory]` that never branches on its arguments (P3).
  3. Sad paths as D14 (P4).
  4. Code-only expected values are marked `characterization: <file:line>` and approved (D8).
  5. Stryker: an Infrastructure run and an Api run are added (D17, D18); test-only changes use full or `--mutate` runs, never `--since:main`; targets and `break` as D19 (P2).
  6. ASP.NET Core Identity's own clock is faked with a hand-written `TimeProvider` (P6; that its `UserManager` reads one from DI is checked in the Infrastructure batch).
  7. PostgreSQL list: until a use case maps a violation to an error, assert the SQLSTATE and the exact constraint name (P7).
  8. No test that can only fail on a rename (P9).
  9. 1–2 reference test classes per project, as "follow these".
  10. The agents link to these sections instead of copying them (P8).
- **D16 (P1, internals).** The new unit projects reach `internal` types through `InternalsVisibleTo`, added to `WattWise.Infrastructure.csproj` and `WattWise.Api.csproj`. This is the refactor's one production change, made in the support batch.

## Mutation testing

- **D17 (P2, Infrastructure).** Stryker also mutates `WattWise.Infrastructure`, with its integration tests (Docker) and, once it exists, `WattWise.Infrastructure.Tests`. Its full baseline is taken in the support batch before any Infrastructure test changes; the batch must not drop below it.
- **D18 (Api).** Stryker mutates `WattWise.Api` with `WattWise.Api.Tests` only (no Docker); its baseline is taken when that project exists, and the target is that baseline or higher.
- **D19 (Q9, targets).** Kill every killable survivor: Domain ≥ 88 % (23 of 26), Application ≥ 68 %, Infrastructure and Api ≥ their baselines. `break` in `backend/stryker-config.json` stays 60 until the final batch, then rises to the lowest final score rounded down to a multiple of 5.

## Agents

- **D20 (Q13).** `backend-test-reviewer` goes back to `model: sonnet` in the final batch.
