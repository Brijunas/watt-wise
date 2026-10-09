# workflows

GitHub Actions workflows for Watt-Wise. The pull request workflow builds, lints and tests all apps. Pushes to `main` and `dev` build, sign and publish the Docker images to GitHub Container Registry and move a deploy tag; the server pulls from there, and no workflow connects to it. See [technical.md](../../docs/technical.md).

There are no workflows yet. Both are created in E5 (see [epics.md](../../docs/epics.md)). GitHub Actions only reads `.yml` and `.yaml` files here, so this README is ignored.
