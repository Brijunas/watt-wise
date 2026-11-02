using Npgsql;

namespace WattWise.Infrastructure.IntegrationTests;

/// <summary>
/// The role matrix of deploy/docs/postgres.md against the real bootstrap and migrations. Expected values are literals from that doc
/// and deploy/postgres/bootstrap.sql; the schema names are written out on purpose, so renaming one in code can't hide a wrong grant.
/// </summary>
public class RoleAccessTests(DatabaseFixture fixture) : IClassFixture<DatabaseFixture>, IAsyncLifetime
{
    private const string PermissionDenied = "42501";
    private const string ReadOnlySqlTransaction = "25006";
    private const string Insert = "INSERT INTO {table} (note) VALUES ({0})";
    private const string Update = "UPDATE {table} SET note = 'changed' WHERE note = {0}";
    private const string Delete = "DELETE FROM {table} WHERE note = {0}";
    private const string CountByNote = "SELECT count(*) AS \"Value\" FROM {table} WHERE note = {0}";

    // Tests in a class run one after another, so the probe tables are created idempotently for each and never raced.
    public async ValueTask InitializeAsync()
    {
        foreach (string schema in new[] { "app", "hangfire", "migrations" })
        {
            await PlatformSql.CreateProbeTableAsync(fixture, schema);
        }
    }

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;

    [Theory]
    [InlineData(DatabaseRole.Api, "app")]
    [InlineData(DatabaseRole.Jobs, "app")]
    [InlineData(DatabaseRole.Jobs, "hangfire")]
    public async Task Role_can_read_rows_in_its_schema(DatabaseRole role, string schema)
    {
        string note = await PlatformSql.SeedProbeRowAsync(fixture, schema);

        List<long> count = await PlatformSql.QueryAsync<long>(fixture, role, Sql(CountByNote, schema), note);

        Assert.Equal([1L], count);
    }

    [Theory]
    [InlineData(DatabaseRole.Api, "app", Insert)]
    [InlineData(DatabaseRole.Api, "app", Update)]
    [InlineData(DatabaseRole.Api, "app", Delete)]
    [InlineData(DatabaseRole.Jobs, "app", Insert)]
    [InlineData(DatabaseRole.Jobs, "app", Update)]
    [InlineData(DatabaseRole.Jobs, "app", Delete)]
    [InlineData(DatabaseRole.Jobs, "hangfire", Insert)]
    [InlineData(DatabaseRole.Jobs, "hangfire", Update)]
    [InlineData(DatabaseRole.Jobs, "hangfire", Delete)]
    public async Task Role_can_change_rows_in_its_schema(DatabaseRole role, string schema, string statement)
    {
        string note = await PlatformSql.SeedProbeRowAsync(fixture, schema);

        int affected = await PlatformSql.ExecuteAsync(fixture, role, Sql(statement, schema), note);

        Assert.Equal(1, affected);
    }

    [Theory]
    [InlineData(DatabaseRole.Api, "hangfire")]
    [InlineData(DatabaseRole.Api, "migrations")]
    [InlineData(DatabaseRole.Jobs, "migrations")]
    public async Task Role_cannot_read_a_schema_it_has_no_grant_on(DatabaseRole role, string schema)
    {
        PostgresException exception = await Assert.ThrowsAsync<PostgresException>(
            () => PlatformSql.QueryAsync<long>(fixture, role, Sql(CountByNote, schema), "any"));

        Assert.Equal(PermissionDenied, exception.SqlState);
    }

    // One statement per pair: a missing USAGE on the schema stops every statement the same way.
    [Theory]
    [InlineData(DatabaseRole.Api, "hangfire", Insert)]
    [InlineData(DatabaseRole.Api, "migrations", Update)]
    [InlineData(DatabaseRole.Jobs, "migrations", Delete)]
    public async Task Role_cannot_change_rows_in_a_schema_it_has_no_grant_on(DatabaseRole role, string schema, string statement)
    {
        PostgresException exception = await Assert.ThrowsAsync<PostgresException>(
            () => PlatformSql.ExecuteAsync(fixture, role, Sql(statement, schema), "any"));

        Assert.Equal(PermissionDenied, exception.SqlState);
    }

    // bootstrap.sql:64 gives reader pg_read_all_data, so it reads migrations too (decision D11).
    [Theory]
    [InlineData("app")]
    [InlineData("hangfire")]
    [InlineData("migrations")]
    public async Task Reader_can_read_every_schema(string schema)
    {
        string note = await PlatformSql.SeedProbeRowAsync(fixture, schema);

        List<long> count = await PlatformSql.QueryAsync<long>(fixture, DatabaseRole.Reader, Sql(CountByNote, schema), note);

        Assert.Equal([1L], count);
    }

    // Its sessions are read-only transactions (bootstrap.sql:70), which PostgreSQL checks before privileges; one statement is
    // enough because the setting is per role, not per schema or statement.
    [Fact]
    public async Task Reader_cannot_write()
    {
        PostgresException exception = await Assert.ThrowsAsync<PostgresException>(
            () => PlatformSql.ExecuteAsync(fixture, DatabaseRole.Reader, Sql(Insert, "app"), "blocked"));

        Assert.Equal(ReadOnlySqlTransaction, exception.SqlState);
    }

    [Theory]
    [InlineData(DatabaseRole.Api, "app")]
    [InlineData(DatabaseRole.Api, "hangfire")]
    [InlineData(DatabaseRole.Api, "migrations")]
    [InlineData(DatabaseRole.Jobs, "app")]
    [InlineData(DatabaseRole.Jobs, "hangfire")]
    [InlineData(DatabaseRole.Jobs, "migrations")]
    public async Task Role_cannot_create_tables(DatabaseRole role, string schema)
    {
        PostgresException exception = await Assert.ThrowsAsync<PostgresException>(
            () => PlatformSql.ExecuteAsync(fixture, role, "CREATE TABLE " + schema + ".ddl_probe (id int)"));

        Assert.Equal(PermissionDenied, exception.SqlState);
    }

    // postgres.md "Schemas and privileges": public belongs to admin and every privilege is revoked from PUBLIC (bootstrap.sql:93-94).
    [Theory]
    [InlineData(DatabaseRole.Api)]
    [InlineData(DatabaseRole.Jobs)]
    [InlineData(DatabaseRole.Cli)]
    public async Task Role_cannot_create_tables_in_public(DatabaseRole role)
    {
        PostgresException exception = await Assert.ThrowsAsync<PostgresException>(
            () => PlatformSql.ExecuteAsync(fixture, role, "CREATE TABLE public.ddl_probe (id int)"));

        Assert.Equal(PermissionDenied, exception.SqlState);
    }

    // The tables below are created by migrate, whose connection is the Cli acting as owner (postgres.md "Roles").
    // characterization: the Hangfire table names are those of Hangfire.PostgreSql's install script, version 1.21.1 (backend/Directory.Packages.props:13).
    [Theory]
    [InlineData("migrations", "__ef_migrations_history")]
    [InlineData("app", "accounts")]
    [InlineData("hangfire", "job")]
    [InlineData("hangfire", "jobqueue")]
    [InlineData("hangfire", "state")]
    public async Task Table_is_owned_by_owner(string schema, string table)
    {
        List<string> owners = await PlatformSql.QueryAsync<string>(
            fixture,
            DatabaseRole.Cli,
            "SELECT tableowner AS \"Value\" FROM pg_tables WHERE schemaname = {0} AND tablename = {1}",
            schema,
            table);

        Assert.Equal(["owner"], owners);
    }

    // bootstrap.sql:101-103 re-asserts the owner of each schema on every run.
    [Theory]
    [InlineData("app")]
    [InlineData("hangfire")]
    [InlineData("migrations")]
    public async Task Schema_is_owned_by_owner(string schema)
    {
        List<string> owners = await PlatformSql.QueryAsync<string>(
            fixture,
            DatabaseRole.Cli,
            "SELECT pg_get_userbyid(nspowner) AS \"Value\" FROM pg_namespace WHERE nspname = {0}",
            schema);

        Assert.Equal(["owner"], owners);
    }

    private static string Sql(string template, string schema) => template.Replace("{table}", PlatformSql.ProbeTable(schema), StringComparison.Ordinal);
}
