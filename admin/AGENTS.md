# Admin Guidelines

## Scope & Hierarchy

Follow the [root guide](../AGENTS.md), then the [shared web guide](../frontend/AGENTS.md). This app has no separate docs or rules directory; the frontend documents own its technical decisions. Read the [admin README](README.md) for purpose and [Claude notes](CLAUDE.md) for the existing shared-guidance pointer.

## Required Documentation

- Before implementation: [architecture](../frontend/docs/architecture.md), [structure](../frontend/docs/structure.md), and [conventions](../frontend/docs/conventions.md).
- Before tests: [testing](../frontend/docs/testing.md).
- Before permissions or authentication changes: root [security](../docs/security.md).
- For commands and checks: root [development](../docs/development.md); use `admin` in pnpm filters.

Record admin technical decisions in the shared frontend docs and link them here when new topics need routing. Keep system and security decisions in their root specifications.
