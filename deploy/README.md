# deploy

Deployment configuration for Watt-Wise: Docker Compose files for the `local`, `staging` and `production` environments, `.env.example` templates that reference 1Password item paths, and the cloudflared tunnel config. See "Hosting and operations" in [technical.md](../docs/technical.md).

The folder is empty for now. S2.3 adds `docker-compose.local.yml` for the local database, and E5 adds the rest (see [epics.md](../docs/epics.md)). Real `.env` files are never committed; secrets come from 1Password at runtime.
