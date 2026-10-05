# Watt-Wise

Watt-Wise helps Lithuanian households find the cheapest electricity setup: it reads ESO hourly consumption history and compares grid and supplier plans against it. See [product.md](docs/product.md).

## Getting started

Follow [docs/setup.md](docs/setup.md) to install the tools and dependencies, run the checks and learn the everyday commands.

## Layout

A monorepo with plain top-level folders and no task runner. The JavaScript side is a pnpm workspace; the backend is a .NET solution built with the `dotnet` CLI.

- `frontend/`: public React + TypeScript Vite PWA.
- `admin/`: internal admin PWA, same stack as `frontend/`.
- `packages/`: private `@wattwise/*` workspace packages shared by the two apps (`api-client`, `ui`, `core`, `i18n`).
- `backend/`: .NET 11 solution with the API and the Hangfire jobs host.
- `deploy/`: Docker Compose files, `.env.example` templates and cloudflared config.
- `.github/workflows/`: CI and CD.
- `docs/`: specifications and developer docs.

## Docs

- [product.md](docs/product.md): what the product does and the MVP scope.
- [technical.md](docs/technical.md): technical decisions, from stacks and architecture to hosting.
- [epics.md](docs/epics.md): the MVP epics and stories.
- [setup.md](docs/setup.md): machine setup, commands and troubleshooting.
- [known-issues.md](docs/known-issues.md): upstream problems and their temporary workarounds.
