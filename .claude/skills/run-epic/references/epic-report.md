# Epic report

Written to `docs/epic-runs/<E>/report.md` before the run records are deleted. It becomes the epic PR's description, so write it for a reviewer who was not there. Use measured numbers from `log.md` and the gate JSON, never estimates.

```markdown
## Summary

<Two or three sentences: what the epic (or range) delivered and the result of the epic gate.>

## Stories

| Story | What it did | Attempts | Fix rounds | Gate | Review |
| ----- | ----------- | -------- | ---------- | ---- | ------ |

## Decisions and amendments

- <decision or amendment, who made it (user answer or logged mechanical change), the doc updated>

## Gate numbers

- Checks on the epic head: <each of the eight, pass/fail, seconds>
- Pending model changes: none | <detail>
- API client in sync: yes | no
- Mutation (full run), per project: <project: score, target, previous score>
- Suppressions added (target 0): <count and list>

## Escalations

<Each parked story or question: the brief title, the answer, the time it waited. "None" if none.>

## Risks and follow-ups

- <what the reviewers flagged and the orchestrator declined, with the reason; known gaps; stories left out of the range>

## Try it

<The stack address from 1Password item names, the flows to try by hand (for example sign-up and email confirmation in Mailpit).>
```
