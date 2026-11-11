# Deployment Guidelines

## Scope & Hierarchy

Follow the [root guide](../AGENTS.md) and the root [technical](../docs/technical.md) and [security](../docs/security.md) specifications before deployment changes. This guide covers Compose, bootstrap SQL, and development service configuration. Local docs refine those system decisions. There is no deployment rules directory.

## Required Documentation

| Read before changing                                              | Source                                                                                                              |
| ----------------------------------------------------------------- | ------------------------------------------------------------------------------------------------------------------- |
| Deployment layout or configuration files                          | [README](README.md)                                                                                                 |
| Database images, roles, grants, bootstrap, or connection security | [PostgreSQL](docs/postgres.md)                                                                                      |
| Grafana, OTLP ports, telemetry storage, or troubleshooting        | [LGTM](docs/lgtm.md), backend [observability](../backend/docs/observability.md)                                     |
| Development SMTP, caught messages, or Mailpit configuration       | [Mailpit](docs/mailpit.md), backend [email](../backend/docs/email.md) and [outbox](../backend/docs/email-outbox.md) |
| First-time infrastructure setup                                   | Root [setup](../docs/setup.md)                                                                                      |
| Starting services or changing runtime settings                    | Root [development](../docs/development.md)                                                                          |
| Schema migrations                                                 | Backend [guide](../backend/AGENTS.md) and [migration skill](../.agents/skills/ef-migration/SKILL.md)                |
| Release automation                                                | [Workflow status](../.github/workflows/README.md) and root [epics](../docs/epics.md)                                |

## Configuration & Verification

Use the commands and role boundaries from these documents. Keep secrets in 1Password, notify the user before authorization, and keep resolved credentials out of committed files. Follow the PostgreSQL document when synchronizing image versions with integration fixtures.

Update the owning service document when behavior changes. Keep environment-wide architecture and security policy in root specifications. Verify the affected service configuration and behavior using the documented workflow; distinguish implemented services from future deployment stories.
