---
name: write-story
description: Write or refine Watt-Wise epics and stories in docs/epics.md. Use whenever the user asks to write, add, split, plan, reword or review a story or an epic, to break an epic (E4, E7…) into stories, or to turn an idea, bug or review finding into a story, even if they don't name the skill. Not for implementing a story.
---

# Write a story

Watt-Wise tracks its work in `docs/epics.md`, not in an issue tracker. A story is one bullet under its epic's `### Stories` heading. Claude reads it cold when the story is planned, so it must make sense without this conversation. The story says what must be true when it ships and how to prove it; the specs say how the system works.

## 1. Gather context

Read before writing. A story that contradicts a spec is wrong, however good it reads.

- `docs/epics.md`: the target epic's paragraph, its other stories, and the epics around it (dependency order).
- `docs/product.md` for anything user-facing: MVP scope, non-goals, open questions.
- `docs/security.md` for anything that touches accounts, tokens, sessions, permissions or limits.
- The section of `docs/technical.md` that covers the area, and the project's own docs (`backend/docs/`, `deploy/docs/`, later `frontend/`, `admin/`, `packages/*`).
- `docs/known-issues.md` if the story touches tool versions or the ESO CSV.
- The code the story builds on, so the story names files and types that really exist.

Don't read `docs/implemented.md` unless the user asks about past work.

## 2. Settle the decisions first

A story never makes an architecture or product decision on its own.

- If the specs already decide it, the story follows them and links the section.
- If the specs don't cover it, ask the user. Once decided, update the spec in the same change and link it from the story.
- If it can't be decided yet, the story says so and says when it will be, e.g. "which endpoint S3.6 uses is decided when S3.6 is planned".
- If the story is about finding the decision (like S2.12), it says what to evaluate and where the result gets written.
- If it would cut across a non-goal in `product.md` (prosumers, OAuth sign-in, price alerts), stop and ask.

## 3. Write the story

### Format

```markdown
- **S<epic>.<n> <Title>.** <Body.> Done: <verifiable outcome>.
```

Shipped stories move to `docs/implemented.md` with their epic, so the S2 examples cited below are there.

**Never change a story that is already implemented.** Before editing any existing story, including one in another epic that a new decision touches, check that it hasn't shipped. A shipped story is in `docs/implemented.md`, has commits on `main` that name its ID (`git log --oneline origin/main | grep 'S3.9'`), or has its code in the repo. An epic still in `epics.md` can have some stories shipped, so check each story, not the epic. If it has shipped, leave it as it is and write the change as a new story in an epic that is still open.

- **ID:** the next free number in the epic. Never reuse or renumber an ID: commits and PRs refer to them, and a dropped story leaves its gap (S2.2).
- **Title:** a short noun phrase in sentence case, ending with a period inside the bold: `**S3.5 i18n.**`.
- **Body:** one paragraph. Use nested bullets only when the story has several separate deliverables (see S2.3).
- **Done:** always last. It is the acceptance criteria (section 4).

### What the body says

1. **Why, when it isn't obvious.** One sentence on the trigger or the user value, as S2.12 did with "The trigger: …". User-facing stories start with what the user can do once it ships, in plain words. Don't use "As a … I want … so that"; the epic already says who the user is.
2. **What gets delivered.** Projects, packages, files, endpoints, screens, jobs and tables, named as the code and the specs name them (`WattWise.Infrastructure`, `@wattwise/ui`, `deploy/docs/postgres.md`). Name libraries and tools only when a spec already chose them.
3. **Who does what, when it isn't all Claude.** Scaffolders whose output must match the current release are run by the developer, then Claude takes over: "The developer runs `pnpm create vite` … Claude then …". Ditto anything that needs the user's 1Password or a manual secret.
4. **Docs the story changes.** A new command → `docs/development.md`. A setup change → `docs/setup.md`. A new or changed decision → the spec that owns it; a security decision → `docs/security.md`. A workaround for an upstream problem → `docs/known-issues.md`.
5. **Links, not repeats.** Each fact lives in one doc. Link it (`[architecture.md](../backend/docs/architecture.md#hosts)`) and add only what the story needs on top.

Leave out how to write the code: class layout, step-by-step instructions, code snippets. The plan made when the story is picked up covers that.

### Size

One story is one PR that one review can follow. Split it when the Done has unrelated parts, it spans backend and frontend without a reason to land them together, or the body needs more than about six bullets. Keep a vertical slice whole when splitting it would leave a half that can't be tested.

## 4. Write the Done

The Done is what the reviewer and the tests check. Every part of it can pass or fail.

- **Prove it with something runnable:** a test that proves a behaviour, a command that goes green, or a manual step with the result it should give.
  - `dotnet test` runs against a real container.
  - A commit with a lint error in either app is rejected locally.
  - Lighthouse reports installable.
- **Say what must not happen** as well as what must: "each role can do only what it should", "API requests are never served from cache", "regenerating produces no diff".
- **Name the test level** when it matters: unit (`Domain.Tests`, `Application.Tests`), integration (Testcontainers, `WebApplicationFactory`), component (Vitest and React Testing Library). The rules are in `backend/docs/testing.md`.
- **Avoid vague words:** "works correctly", "is fast", "is intuitive". Name the observable result instead.
- **The checks are a given.** Every story ends with the checks in `docs/development.md` green and the backend building with 0 warnings, so don't repeat them unless the story changes the checks.

### Coverage checklist

Go through it and include whatever applies. Leave out what doesn't.

- **Happy path:** the main flow, end to end through the layers the story touches.
- **Validation and errors:** invalid input returns a `Validation` error as ProblemDetails (`backend/docs/error-handling.md`); the UI shows localized messages.
- **Permissions:** what an anonymous user, another account's user and a non-admin get (E4 onward). Data is always scoped to the account (`docs/security.md`).
- **Time:** hours stored as UTC, shown and zoned in Europe/Vilnius, with 23- and 25-hour DST days covered by tests.
- **Data:** what is persisted and where, upsert or merge on repeated input, what is discarded (the CSV after import), what export and account deletion must include (E15).
- **UI:** works at `xs` and from `md` up (mobile-first), light and dark, LT and EN.
- **Jobs and ingestion:** safe to rerun, no duplicates on rerun, gaps found and filled, failures logged.
- **Security and limits:** the rules in `docs/security.md` that the story touches (sessions, tokens, account discovery, rate and size limits), and no secrets in the repo (values come from 1Password through `op run`).
- **Ops:** health, logs and traces, migrations run only through `WattWise.Cli migrate`.

## 5. Writing an epic

An epic is a `## E<n>. Title` heading with one paragraph, then `### Stories` once it is broken down. Later epics stay a paragraph until they are planned.

- The paragraph says what the epic delivers end to end, what it depends on and why it sits where it does in the order.
- Open questions it resolves are named and linked to `product.md` or `known-issues.md`.
- Its stories are ordered so each one builds on the ones above it.
- Update `CLAUDE.md`'s "Project state" only when a story ships, not when it is written.

## 6. Check before handing over

- Every claim matches the specs. Every link resolves, anchors included.
- The ID is new, the story is in the right epic, and the order still respects dependencies.
- The Done can be checked by someone who wasn't in the conversation.
- `docs/epics.md` stays at 200 lines or fewer. If it would go over, ask how to split it.
- Run `pnpm format:check`.

Show the user the story and leave it uncommitted for review.
