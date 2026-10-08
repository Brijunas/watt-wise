-- Creates the Watt-Wise database, roles, schemas and grants.
--
-- Idempotent: safe to re-run. Missing objects are created, role settings and grants are
-- re-applied.
--
-- Sets no passwords: login roles are created without one and can't log in until an
-- admin sets it with psql's \password (sent hashed, so it never reaches the server
-- log). See deploy/docs/postgres.md.
--
-- Run by hand as the superuser `admin`, connected to the `postgres` database, in every
-- environment. Development: see "Development database" in docs/setup.md.
--
-- Roles: admin (superuser, created by the image), owner (NOLOGIN, owns the database
-- and schemas), cli (migrations, SET ROLE owner), api, jobs, backup.

\set ON_ERROR_STOP on

-- Roles: CREATE ROLE has no IF NOT EXISTS, so each one is generated conditionally and
-- executed with \gexec.
SELECT 'CREATE ROLE "owner" NOLOGIN'
WHERE NOT EXISTS (SELECT FROM pg_roles WHERE rolname = 'owner')
\gexec

SELECT 'CREATE ROLE cli LOGIN'
WHERE NOT EXISTS (SELECT FROM pg_roles WHERE rolname = 'cli')
\gexec

SELECT 'CREATE ROLE api LOGIN'
WHERE NOT EXISTS (SELECT FROM pg_roles WHERE rolname = 'api')
\gexec

SELECT 'CREATE ROLE jobs LOGIN'
WHERE NOT EXISTS (SELECT FROM pg_roles WHERE rolname = 'jobs')
\gexec

SELECT 'CREATE ROLE backup LOGIN'
WHERE NOT EXISTS (SELECT FROM pg_roles WHERE rolname = 'backup')
\gexec

-- cli can SET ROLE owner but does not inherit its privileges implicitly.
GRANT "owner" TO cli WITH INHERIT FALSE, SET TRUE;
GRANT pg_read_all_data TO backup;

ALTER ROLE api SET statement_timeout = '30s';
ALTER ROLE api SET idle_in_transaction_session_timeout = '60s';
ALTER ROLE jobs SET statement_timeout = '5min';
ALTER ROLE jobs SET idle_in_transaction_session_timeout = '60s';
-- search_path: api sees only app, jobs sees app and hangfire, so unqualified names work.
ALTER ROLE api SET search_path = app;
ALTER ROLE jobs SET search_path = app, hangfire;

-- Database.
SELECT 'CREATE DATABASE wattwise OWNER "owner"'
WHERE NOT EXISTS (SELECT FROM pg_database WHERE datname = 'wattwise')
\gexec
-- Re-asserted on every run, in case the database already existed (e.g. created by the image).
ALTER DATABASE wattwise OWNER TO "owner";

-- Nobody connects by default; admin is a superuser and is unaffected.
REVOKE ALL ON DATABASE wattwise FROM PUBLIC;
REVOKE ALL ON DATABASE postgres FROM PUBLIC;
REVOKE ALL ON DATABASE template1 FROM PUBLIC;
GRANT CONNECT ON DATABASE wattwise TO cli, api, jobs, backup;

\connect wattwise

-- public would otherwise belong to pg_database_owner, i.e. owner, letting migrations
-- create objects there. Give it to the superuser running this script instead.
ALTER SCHEMA public OWNER TO CURRENT_USER;
REVOKE ALL ON SCHEMA public FROM PUBLIC;
CREATE SCHEMA IF NOT EXISTS app AUTHORIZATION "owner";
CREATE SCHEMA IF NOT EXISTS hangfire AUTHORIZATION "owner";
-- Holds EF Core's migration history. Nothing is granted on it, so only owner (the Cli)
-- can reach it.
CREATE SCHEMA IF NOT EXISTS migrations AUTHORIZATION "owner";
-- Re-asserted on every run, in case a schema already existed with another owner.
ALTER SCHEMA app OWNER TO "owner";
ALTER SCHEMA hangfire OWNER TO "owner";
ALTER SCHEMA migrations OWNER TO "owner";

-- Extension lives in public, which only admin can use; no other role gets USAGE on
-- public. Statistics views are read by admin.
CREATE EXTENSION IF NOT EXISTS pg_stat_statements SCHEMA public;

GRANT USAGE ON SCHEMA app TO api, jobs;
GRANT USAGE ON SCHEMA hangfire TO jobs;

-- Table grants come only from default privileges on objects owner creates (migrations
-- run as owner). No GRANT ... ON ALL TABLES here: the bootstrap runs before any table
-- exists, and a re-run must not undo revokes a migration makes on a single table.
ALTER DEFAULT PRIVILEGES FOR ROLE "owner" IN SCHEMA app
  GRANT SELECT, INSERT, UPDATE, DELETE ON TABLES TO api, jobs;
ALTER DEFAULT PRIVILEGES FOR ROLE "owner" IN SCHEMA app
  GRANT USAGE, SELECT ON SEQUENCES TO api, jobs;
ALTER DEFAULT PRIVILEGES FOR ROLE "owner" IN SCHEMA hangfire
  GRANT SELECT, INSERT, UPDATE, DELETE ON TABLES TO jobs;
ALTER DEFAULT PRIVILEGES FOR ROLE "owner" IN SCHEMA hangfire
  GRANT USAGE, SELECT ON SEQUENCES TO jobs;
