# PostgreSQL

How the Watt-Wise database is set up in every environment: the roles, what each one may do, how they are created and how the hardening works. The environments themselves are described under "Environments" in [technical.md](../../docs/technical.md); EF Core and migrations inside the backend are in [backend/docs/architecture.md](../../backend/docs/architecture.md#persistence).

## Version

PostgreSQL 18.6, the official `postgres:18.6-trixie` image. The same tag is pinned in the compose files and in the integration-test fixture (`PostgresContainerFixture` in `backend/tests/WattWise.Testing`); change them together. PostgreSQL 19 was still in beta when this was decided (2026-10). Moving to a new major is a planned upgrade, not a tag bump: data from an older major has to be migrated (`pg_upgrade` or dump and restore).

## Roles

Each environment has its own cluster, so role names carry no prefix.

| Role     | Login | Used by                        | May do                                                                                                                       |
| -------- | ----- | ------------------------------ | ---------------------------------------------------------------------------------------------------------------------------- |
| `admin`  | yes   | People (and Claude) only       | Superuser. Creates roles and databases, runs the bootstrap, fixes things by hand. Never put its password in an app's config. |
| `owner`  | no    | Nobody logs in as it           | Owns the `wattwise` database, the `app`, `hangfire` and `migrations` schemas and every table in them.                        |
| `cli`    | yes   | `WattWise.Cli`                 | Nothing on its own. It switches to `owner` (`SET ROLE owner`) to run migrations, scripts and maintenance.                    |
| `api`    | yes   | `WattWise.Api`                 | Read and write rows in schema `app`. No DDL, no access to `hangfire` or `migrations`.                                        |
| `jobs`   | yes   | `WattWise.Jobs`                | Read and write rows in schemas `app` and `hangfire`. No DDL, no access to `migrations`.                                      |
| `backup` | yes   | `pg_dump` (backups come in E6) | Read every table (`pg_read_all_data`), nothing else.                                                                         |

### Why `owner` and `cli` are separate

`owner` exists so that every object has one owner no matter who ran the migration. PostgreSQL default privileges are attached to the role that creates an object (`ALTER DEFAULT PRIVILEGES FOR ROLE owner`), so a table created as `owner` immediately gets the grants for `api` and `jobs`. If `cli` created tables as itself, it would own them, the defaults would not fire and the apps would get "permission denied".

`cli` is a member of `owner` with `INHERIT FALSE, SET TRUE`: it doesn't get `owner`'s rights automatically, it has to switch explicitly. Its connection does that on connect: the `postgres-cli` item's connection options are `-c role=owner`, so every session of the Cli acts as `owner`, and a session without the switch can't touch any table.

`admin` is different: it is a superuser that bypasses every permission check. It exists for people, and no application ever connects as it.

## Schemas and privileges

| Schema       | Owner   | Contents                                            | Granted to                                                                                          |
| ------------ | ------- | --------------------------------------------------- | --------------------------------------------------------------------------------------------------- |
| `app`        | `owner` | Application tables                                  | `api` and `jobs`: `USAGE`; `SELECT, INSERT, UPDATE, DELETE` on tables; `USAGE, SELECT` on sequences |
| `hangfire`   | `owner` | Hangfire storage                                    | `jobs`: the same set                                                                                |
| `migrations` | `owner` | The EF Core history table `__ef_migrations_history` | Nobody: only `owner`, so only the Cli, reads or writes migration history                            |
| `public`     | `admin` | Only the `pg_stat_statements` extension             | Nothing: every privilege is revoked from `PUBLIC`                                                   |

- **Why `jobs` reaches `app`.** Jobs run Application use cases, which read and write app data through `AppDbContext` like the Api's endpoints do ([architecture.md](../../backend/docs/architecture.md#hosts)). It has its own role rather than reusing `api`'s, so the Api still can't see Hangfire's tables and each connection shows which host made it.
- **Why `migrations` is separate.** The default privileges on `app` would give every app role access to the history table, and each one would need a revoke in a migration. In its own schema with no grants, no app role ever sees it.
- The grants are default privileges `FOR ROLE owner`, so they apply to every table a migration creates. The bootstrap doesn't grant on existing tables, so a re-run never undoes a revoke a migration makes on one table.
- `CONNECT` and `TEMPORARY` are revoked from `PUBLIC` on `wattwise`, `postgres` and `template1`. `CONNECT` on `wattwise` is granted only to `cli`, `api`, `jobs` and `backup`.
- `public` would by default belong to `pg_database_owner`, which is `owner` here. The bootstrap gives it to `admin`, so migrations can't create objects in it.
- The bootstrap re-asserts the owner of the `wattwise` database and the `app`, `hangfire` and `migrations` schemas on every run, so they belong to `owner` even if they existed before.
- The apps never create or alter tables. Schema changes go through `WattWise.Cli`, which runs before Api and Jobs start; how Hangfire's tables fit in is under "Background jobs" in [architecture.md](../../backend/docs/architecture.md#background-jobs).

## Hardening

| Setting                               | Value                                                                                                                                                                                                                                                |
| ------------------------------------- | ---------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| Connections                           | No per-role limit and no pool settings: Npgsql's default pool (up to 100 per pool) and the server's `max_connections` (100) apply. Revisit if a host ever exhausts the server.                                                                       |
| `statement_timeout`                   | `api` 30 s, `jobs` 5 min, `cli` none                                                                                                                                                                                                                 |
| `idle_in_transaction_session_timeout` | `api` and `jobs` 60 s                                                                                                                                                                                                                                |
| Password hashing                      | `scram-sha-256` (the default; MD5 is deprecated in 18)                                                                                                                                                                                               |
| Query statistics                      | `pg_stat_statements` preloaded and created in `wattwise`                                                                                                                                                                                             |
| Audit logging                         | `log_connections`, `log_disconnections`, `log_statement = ddl` (pgaudit isn't in the official image)                                                                                                                                                 |
| Network                               | Development publishes the port only on the loopback address in the `postgres-admin` item. Deployed environments publish no port at all.                                                                                                              |
| Client authentication                 | The image's `pg_hba.conf`: `scram-sha-256` for every connection from outside the container, `trust` for the socket and loopback inside it. Only someone who can already `docker exec` (root-equivalent) gets in without a password. Kept on purpose. |

## Bootstrap

[`deploy/postgres/bootstrap.sql`](../postgres/bootstrap.sql) creates everything above except `admin`, which the image creates from `POSTGRES_USER`. An admin runs it by hand, once per database, in every environment:

- it is idempotent, so it is safe to run again after a change to it;
- it runs as `admin` against the `postgres` database;
- it sets no passwords. Login roles are created without one and can't log in until an admin sets it.

Development steps are under "Development database" in [setup.md](../../docs/setup.md). Testing, Staging and Production follow the same steps against their own vault (E5). After the bootstrap, `WattWise.Cli migrate` creates and updates the tables.

## Passwords

Docker never handles application passwords. Each password belongs to one role and reaches only the process that uses it.

- **Storage:** every role's password is a Database item in the environment's 1Password vault. `Watt Wise Development` holds `postgres-admin`, `postgres-api`, `postgres-jobs`, `postgres-cli` and `postgres-backup`, plus `pgadmin` for the pgAdmin container. The non-database items `api`, `jobs`, `frontend` and `admin` hold the apps' URLs, and `grafana` and `otlp` the Grafana LGTM addresses ([lgtm.md](lgtm.md)).
- **Setting one:** an admin sets it on the role with psql's `\password <role>`, pasting the value from 1Password. psql hashes it (SCRAM) before sending, so the plain password never reaches the server or its DDL log, unlike `ALTER ROLE ... PASSWORD '...'`.
- **Rotating one:** change it in 1Password, then run `\password <role>` again.
- **Apps:** nothing about the connection is in the repo. Each process gets every connection field (`Database__Host`, `__Port`, `__Name`, `__Username`, `__Password`, `__Options`) from its role's 1Password item, through its own env file and `op run`. Infrastructure builds the connection string from them.
- **Reference files** hold only `op://` references. Each app has its own, next to its project, per environment (`backend/src/WattWise.Api/.env.development`, `backend/src/WattWise.Cli/.env.development`, `backend/src/WattWise.Jobs/.env.development`). Each maps the `Database__*` fields of that app's own role only. The Api's also sets `ASPNETCORE_URLS` from the `api` item and its CORS origins from the `frontend` and `admin` items, and the Jobs host's sets `ASPNETCORE_URLS` from the `jobs` item. Docker's own files are in [`deploy/development/`](../development/):
  - `compose.settings.tpl`: the stack's non-secret settings from `postgres-admin` (bind address, published port, superuser name, maintenance database) and `pgadmin` (bind address, port, login email), and the Grafana and OTLP bind addresses and ports from `grafana` and `otlp`. `op inject` renders it into the gitignored `deploy/.env`, which Compose reads automatically, so everyday compose commands need no `op run`. No password is ever rendered to disk.
  - `compose.env`: the `admin` and pgAdmin passwords, through `op run`, for the first start only. The images read them only to initialize empty volumes. Containers recreated later run with empty password variables.

## pgAdmin

The Development compose stack runs pgAdmin on the address and port in the `pgadmin` item, in desktop mode (no pgAdmin login). The server "Watt-Wise (Development)" is registered for user `admin`, and pgAdmin asks for the password the first time. Copy it from the `postgres-admin` item. `servers.json` is a static file that pgAdmin reads itself, so it can't use 1Password references. It holds only the compose service name, the container-internal port and the superuser name. pgAdmin runs only in Development; deployed environments are reached through an SSH tunnel.
