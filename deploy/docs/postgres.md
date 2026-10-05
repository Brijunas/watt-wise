# PostgreSQL

How the Watt-Wise database is set up in every environment: the roles, what each one may do, how they are created and how the hardening works. The environments themselves are described under "Environments" in [technical.md](../../docs/technical.md); EF Core and migrations inside the backend are in [backend/docs/architecture.md](../../backend/docs/architecture.md#persistence).

## Version

PostgreSQL 18.6, the official `postgres:18.6-trixie` image. PostgreSQL 19 was still in beta when this was decided (2026-10). Moving to a new major is a planned upgrade, not a tag bump: data from an older major has to be migrated (`pg_upgrade` or dump and restore).

## Roles

Each environment has its own cluster, so role names carry no prefix.

| Role       | Login | Used by                        | May do                                                                                                                       |
| ---------- | ----- | ------------------------------ | ---------------------------------------------------------------------------------------------------------------------------- |
| `admin`    | yes   | People (and Claude) only       | Superuser. Creates roles and databases, runs the bootstrap, fixes things by hand. Never put its password in an app's config. |
| `owner`    | no    | Nobody logs in as it           | Owns the `wattwise` database, the `app` and `hangfire` schemas and every table in them.                                      |
| `cli`      | yes   | `WattWise.Cli`                 | Nothing on its own. It switches to `owner` (`SET ROLE owner`) to run migrations, scripts and maintenance.                    |
| `api`      | yes   | `WattWise.Api`                 | Read and write rows in schema `app`. No DDL, no access to `hangfire`.                                                        |
| `hangfire` | yes   | `WattWise.Jobs` (Hangfire)     | Read and write rows in schema `hangfire`. No DDL, no access to `app` (job code reaches app data through the Api's layers).   |
| `backup`   | yes   | `pg_dump` (backups come in E6) | Read every table (`pg_read_all_data`), nothing else.                                                                         |

### Why `owner` and `cli` are separate

`owner` exists so that every object has one owner no matter who ran the migration. PostgreSQL default privileges are attached to the role that creates an object (`ALTER DEFAULT PRIVILEGES FOR ROLE owner`), so a table created as `owner` immediately gets the grants for `api` or `hangfire`. If `cli` created tables as itself, it would own them, the defaults would not fire and the apps would get "permission denied".

`cli` is a member of `owner` with `INHERIT FALSE, SET TRUE`: it doesn't get `owner`'s rights automatically, it has to switch explicitly. Its connection does that on connect: the `postgres-cli` item's connection options are `-c role=owner`, so every session of the Cli acts as `owner`, and a session without the switch can't touch any table.

`admin` is different: it is a superuser that bypasses every permission check. It exists for people, and no application ever connects as it.

## Schemas and privileges

| Schema     | Owner   | Contents                                                                   | Granted to                                                                               |
| ---------- | ------- | -------------------------------------------------------------------------- | ---------------------------------------------------------------------------------------- |
| `app`      | `owner` | Application tables and the EF Core history table `__ef_migrations_history` | `api`: `USAGE`; `SELECT, INSERT, UPDATE, DELETE` on tables; `USAGE, SELECT` on sequences |
| `hangfire` | `owner` | Hangfire storage, installed by `WattWise.Cli` (S2.9)                       | `hangfire`: the same set                                                                 |
| `public`   | `admin` | Only the `pg_stat_statements` extension                                    | Nothing: every privilege is revoked from `PUBLIC`                                        |

- The grants are default privileges `FOR ROLE owner`, so they apply to every table a migration creates. The bootstrap doesn't grant on existing tables, so a re-run never undoes a revoke made by a migration. The Initial migration revokes `api`'s access to `__ef_migrations_history`, so only the Cli touches migration history.
- `CONNECT` and `TEMPORARY` are revoked from `PUBLIC` on `wattwise`, `postgres` and `template1`. `CONNECT` on `wattwise` is granted only to `cli`, `api`, `hangfire` and `backup`.
- The apps never create or alter tables. Schema changes, including Hangfire's own tables, go through `WattWise.Cli`, which runs before Api and Jobs start. Hangfire runs with `PrepareSchemaIfNecessary = false`.

## Hardening

| Setting                               | Value                                                                                                                                                                                                                                                |
| ------------------------------------- | ---------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| Connection limits                     | `api` 30, `hangfire` 20, `cli` 3, `backup` 2. Keep each app's Npgsql `Maximum Pool Size` below its limit.                                                                                                                                            |
| `statement_timeout`                   | `api` 30 s, `hangfire` 5 min, `cli` none                                                                                                                                                                                                             |
| `idle_in_transaction_session_timeout` | `api` and `hangfire` 60 s                                                                                                                                                                                                                            |
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

- **Storage:** every role's password is a Database item in the environment's 1Password vault. `Watt Wise Development` holds `postgres-admin`, `postgres-api`, `postgres-hangfire`, `postgres-cli` and `postgres-backup`, plus `pgadmin` for the pgAdmin container.
- **Setting one:** an admin sets it on the role with psql's `\password <role>`, pasting the value from 1Password. psql hashes it (SCRAM) before sending, so the plain password never reaches the server or its DDL log, unlike `ALTER ROLE ... PASSWORD '...'`.
- **Rotating one:** change it in 1Password, then run `\password <role>` again.
- **Apps:** nothing about the connection is in the repo. Each process gets every connection field (`Database__Host`, `__Port`, `__Name`, `__Username`, `__Password`, `__Options`) from its role's 1Password item, through its own env file and `op run`. Infrastructure builds the connection string from them.
- **Reference files** hold only `op://` references. Each app has its own, next to its project, per environment (`backend/src/WattWise.Api/.env.development`, `backend/src/WattWise.Cli/.env.development`, and `WattWise.Jobs` from S2.9). Each maps the `Database__*` fields of that app's own role only, and the Api's also sets `ASPNETCORE_URLS` from the `api` item. Docker's own files are in [`deploy/development/`](../development/):
  - `compose.settings.tpl`: the stack's non-secret settings from `postgres-admin` (bind address, published port, superuser name, maintenance database) and `pgadmin` (bind address, port, login email). `op inject` renders it into the gitignored `deploy/.env`, which Compose reads automatically, so everyday compose commands need no `op run`. No password is ever rendered to disk.
  - `compose.env`: the `admin` and pgAdmin passwords, through `op run`, for the first start only. The images read them only to initialize empty volumes. Containers recreated later run with empty password variables.

## pgAdmin

The Development compose stack runs pgAdmin on the address and port in the `pgadmin` item, in desktop mode (no pgAdmin login). The server "Watt-Wise (Development)" is registered for user `admin`, and pgAdmin asks for the password the first time. Copy it from the `postgres-admin` item. `servers.json` is a static file that pgAdmin reads itself, so it can't use 1Password references. It holds only the compose service name, the container-internal port and the superuser name. pgAdmin runs only in Development; deployed environments are reached through an SSH tunnel.
