---
name: ef-migration
description: Turn an EF Core model change in Watt-Wise into a migration, either by regenerating Initial or by adding a new migration, and apply it to the Development database.
disable-model-invocation: true
---

# EF migration

`<infra>` below means `--project backend/src/WattWise.Infrastructure`. Never run
`dotnet ef database update`: only `WattWise.Cli migrate` changes the schema (a hook blocks it).

1. **Check there is a change.** `dotnet ef migrations has-pending-model-changes <infra>`. If it
   reports none, stop.
2. **Ask the user** whether to regenerate `Initial` or add a new migration. Ask every time:
   until the user says the "single Initial migration" period is over, don't assume.
3. **Regenerate Initial:**
   1. Delete `backend/src/WattWise.Infrastructure/Persistence/Migrations/*` (the migration, its Designer and the snapshot).
   2. `dotnet ef migrations add Initial <infra> --output-dir Persistence/Migrations`
   3. Reset the Development tables: the `reset-app-tables.sql` command in `docs/development.md#migrations`.
4. **New migration:** `dotnet ef migrations add <PascalCaseName> <infra> --output-dir Persistence/Migrations`.
   Hand edits go only in the migration's `Up`/`Down` (e.g. a revoke on one table); never in `*.Designer.cs` or the snapshot.
5. **Review the SQL:** `dotnet ef migrations script --idempotent <infra> -o <scratchpad>/migration.sql`
   and read it: names in snake_case in schema `app`, `timestamptz` for `Instant`, the expected indexes and constraints.
6. **Apply:** `op run --env-file backend/src/WattWise.Cli/.env.development -- dotnet run --project backend/src/WattWise.Cli -- migrate`.
7. **Verify:** `has-pending-model-changes` passes; `dotnet test --project backend/tests/WattWise.Infrastructure.IntegrationTests` passes (`InitialMigrationTests`, `RoleAccessTests`).
