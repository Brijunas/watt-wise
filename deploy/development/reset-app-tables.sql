-- Drops every table in the `app` schema and clears EF Core's migration history, so the next
-- `WattWise.Cli migrate` creates the tables again from the regenerated `Initial` migration.
--
-- Development only, and only until the first deploy: see "Persistence" in
-- backend/docs/architecture.md. The schemas, roles and grants from bootstrap.sql stay, and so
-- do Hangfire's tables. Every row in `app` is lost.
--
-- Run by hand as the superuser `admin`, connected to the `wattwise` database: see
-- "Migrations" in docs/development.md.

\set ON_ERROR_STOP on

DO $$
DECLARE
    t text;
BEGIN
    FOR t IN SELECT tablename FROM pg_tables WHERE schemaname = 'app' LOOP
        EXECUTE format('DROP TABLE app.%I CASCADE', t);
    END LOOP;
END
$$;

TRUNCATE migrations.__ef_migrations_history;
