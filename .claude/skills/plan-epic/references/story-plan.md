# Story plan template

One file per node: `docs/epic-runs/<E>/S<id>.md` (`S4.3-backend.md` for a split part). The coder reads it cold; the test writer derives its tests from the acceptance criteria; the reviewers judge against it. Follow the vocabulary of the owning feature doc (`backend/docs/features/`).

```markdown
# S<id> <Title>

Part: backend | frontend (a story touching both is split first)
Depends on: <node IDs or none>
Source: <epics.md story | adopted from docs/<plan>.md>

## Goal

<Two or three sentences: what is true when this story is merged. Written so a hook can judge a
finished evidence table against it. No steps.>

## Acceptance criteria

Numbered. Each one passes or fails by a test, command or observable result, and names the test
level (unit, integration, component). Include what must not happen.

1. <criterion> (level)

## Files per project

| Project | Folder | New or changed |
| ------- | ------ | -------------- |

Registration, folders and naming follow the project's architecture and conventions docs; link the
rule instead of repeating it.

## Test plan

What the test writer writes first, from the criteria: one row per test or test group, the project
it lives in, and the criterion number it proves. Cover the Done checklist items that apply (happy
path, validation and errors, permissions, time, data, jobs, limits). Tests are written before the
code and the coder cannot edit them.

| Criterion | Test (project, class) | Notes |
| --------- | --------------------- | ----- |

## Security checklist

Tick or write "n/a: <reason>" for each. Source: docs/security.md.

- [ ] Data is scoped to the account; other accounts' data is unreachable
- [ ] Anonymous and non-permitted callers get the documented response
- [ ] No account discovery through status, body or timing
- [ ] Rate and size limits that apply are in place
- [ ] No secret, token or address is stored or logged in clear
- [ ] Input is validated at the boundary; errors are ProblemDetails
- [ ] Settings come from 1Password, nothing hard-coded

## Migration

One line: `none`, `regenerate Initial` (until the MVP is released to Production) or
`new migration <Name>` (after). Say whether the story sets `modelChange` in state.json.

## Docs to update

<Docs this story changes, with the section; docs that own each fact.>

## Done evidence

What the coder's evidence table must contain: the check commands and their results, one row per
acceptance criterion with the test that proves it, and the migration state.
```

The first line of a coder's report is `STATUS: done|needs-decision|blocked`, followed by this evidence table.
