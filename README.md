# Watt-Wise

Watt-Wise helps Lithuanian households find the cheapest electricity setup: it reads ESO hourly consumption history and compares grid and supplier plans against it. See [product.md](docs/product.md).

## Getting started

- New machine: follow [setup.md](docs/setup.md) to install the tools and dependencies.
- Everyday work: [development.md](docs/development.md) lists the commands and checks.

## Layout

A monorepo with plain top-level folders and no task runner: a pnpm workspace for the web apps and shared packages, and a .NET solution for the backend. The folder tree is under "Repository" in [technical.md](docs/technical.md).

## Docs

- [product.md](docs/product.md): what the product does and the MVP scope.
- [technical.md](docs/technical.md): whole-system technical decisions, from stacks and architecture to hosting.
- [backend/docs/](backend/docs/): backend architecture, testing and build conventions.
- [epics.md](docs/epics.md): the epics and stories still to do.
- [implemented.md](docs/implemented.md): finished epics, kept for history.
- [setup.md](docs/setup.md): first-time machine setup and troubleshooting.
- [development.md](docs/development.md): everyday commands, dependency updates and the commit hook.
- [known-issues.md](docs/known-issues.md): upstream problems and their temporary workarounds.
