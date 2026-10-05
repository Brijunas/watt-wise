# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project state

Watt-Wise is a greenfield project. E1 is done: the repo has the specifications plus the root JS tooling, git hooks, the four placeholder `@wattwise/*` packages and placeholder app folders. No application code exists yet.

## Docs

Each doc has one job. Read or update the one that matches the task:

- `docs/product.md`: what the product does, MVP scope, non-goals, open questions. Source of truth.
- `docs/technical.md`: every technical decision (repo layout, stacks, architecture, data model, time handling, hosting, secrets, code style). Source of truth; read the relevant section before implementing, and every implementation decision must match it.
- `docs/epics.md`: the epics and stories still to do.
- `docs/implemented.md`: finished epics, kept for history. Don't read it unless asked about past work.
- `docs/setup.md`: first-time machine setup only. Update it only when a story changes how a new machine is set up.
- `docs/development.md`: everyday commands and workflow. Update it when a story adds or changes a command.
- `docs/known-issues.md`: upstream problems with temporary workarounds. Check it before changing tool versions.

## Working rules

- When a task needs a decision the specs don't cover, ask rather than invent. When a decision changes, update the relevant spec in the same change.
- Each fact lives in one doc; other docs link to it instead of repeating it.
- Keep every doc at 200 lines or fewer. Split a doc before it goes over.
- When an epic's last story ships, move the epic from `docs/epics.md` to `docs/implemented.md` in the same change.

## Commands

@docs/development.md
