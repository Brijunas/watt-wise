using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

using Npgsql;

using WattWise.Infrastructure.Persistence;
using WattWise.Infrastructure.Persistence.Schema;

namespace WattWise.Infrastructure.IntegrationTests;

public class RoleAccessTests(DatabaseFixture fixture) : IClassFixture<DatabaseFixture>, IAsyncLifetime
{
    private const string PermissionDenied = "42501";
    private static readonly string[] ProbeSchemas = [DatabaseSchemas.App, DatabaseSchemas.Hangfire, DatabaseSchemas.Migrations];

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    // Tests in a class run one after another, so the probe tables are created idempotently for each and never raced.
    public async ValueTask InitializeAsync()
    {
        await using ServiceProvider provider = fixture.BuildServices(DatabaseRole.Cli);
        await using AsyncServiceScope scope = provider.CreateAsyncScope();
        AppDbContext db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        foreach (string schema in ProbeSchemas)
        {
            await db.Database.ExecuteSqlRawAsync(Sql("CREATE TABLE IF NOT EXISTS {schema}.access_probe (id int)", schema), Token);
        }
    }

    // Schema names can't be SQL parameters, so the statements are templates filled from the DatabaseSchemas constants.
    private static string Sql(string template, string schema, int id = 0) =>
        template.Replace("{schema}", schema, StringComparison.Ordinal)
            .Replace("{id}", id.ToString(System.Globalization.CultureInfo.InvariantCulture), StringComparison.Ordinal)
            .Replace("{newId}", (id + 500).ToString(System.Globalization.CultureInfo.InvariantCulture), StringComparison.Ordinal);

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;

    [Theory]
    [InlineData(DatabaseRole.Api, DatabaseSchemas.App)]
    [InlineData(DatabaseRole.Jobs, DatabaseSchemas.App)]
    [InlineData(DatabaseRole.Jobs, DatabaseSchemas.Hangfire)]
    public async Task Role_reads_and_writes_its_schema(DatabaseRole role, string schema)
    {
        await using ServiceProvider provider = fixture.BuildServices(role);
        await using AsyncServiceScope scope = provider.CreateAsyncScope();
        AppDbContext db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        int id = 1000 + ((int)role * 10) + Array.IndexOf(ProbeSchemas, schema);

        int inserted = await db.Database.ExecuteSqlRawAsync(Sql("INSERT INTO {schema}.access_probe (id) VALUES ({id})", schema, id), Token);
        int selected = await db.Database.SqlQueryRaw<int>(Sql("SELECT id AS \"Value\" FROM {schema}.access_probe WHERE id = {id}", schema, id)).SingleAsync(Token);
        int updated = await db.Database.ExecuteSqlRawAsync(Sql("UPDATE {schema}.access_probe SET id = {newId} WHERE id = {id}", schema, id), Token);
        int deleted = await db.Database.ExecuteSqlRawAsync(Sql("DELETE FROM {schema}.access_probe WHERE id = {newId}", schema, id), Token);

        Assert.Equal(1, inserted);
        Assert.Equal(id, selected);
        Assert.Equal(1, updated);
        Assert.Equal(1, deleted);
    }

    [Theory]
    [InlineData(DatabaseRole.Api, DatabaseSchemas.Hangfire)]
    [InlineData(DatabaseRole.Api, DatabaseSchemas.Migrations)]
    [InlineData(DatabaseRole.Jobs, DatabaseSchemas.Migrations)]
    public async Task Role_cannot_read_another_schema(DatabaseRole role, string schema)
    {
        await using ServiceProvider provider = fixture.BuildServices(role);
        await using AsyncServiceScope scope = provider.CreateAsyncScope();
        AppDbContext db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        PostgresException exception = await Assert.ThrowsAsync<PostgresException>(
            () => db.Database.SqlQueryRaw<long>(Sql("SELECT count(*) AS \"Value\" FROM {schema}.access_probe", schema)).ToListAsync(Token));

        Assert.Equal(PermissionDenied, exception.SqlState);
    }

    [Theory]
    [InlineData(DatabaseRole.Api, DatabaseSchemas.App)]
    [InlineData(DatabaseRole.Api, DatabaseSchemas.Hangfire)]
    [InlineData(DatabaseRole.Api, DatabaseSchemas.Migrations)]
    [InlineData(DatabaseRole.Jobs, DatabaseSchemas.App)]
    [InlineData(DatabaseRole.Jobs, DatabaseSchemas.Hangfire)]
    [InlineData(DatabaseRole.Jobs, DatabaseSchemas.Migrations)]
    public async Task Role_cannot_create_tables(DatabaseRole role, string schema)
    {
        await using ServiceProvider provider = fixture.BuildServices(role);
        await using AsyncServiceScope scope = provider.CreateAsyncScope();
        AppDbContext db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        PostgresException exception = await Assert.ThrowsAsync<PostgresException>(
            () => db.Database.ExecuteSqlRawAsync(Sql("CREATE TABLE {schema}.ddl_probe (id int)", schema), Token));

        Assert.Equal(PermissionDenied, exception.SqlState);
    }

    [Fact]
    public async Task Migration_history_lives_in_its_own_schema_owned_by_owner()
    {
        await using ServiceProvider provider = fixture.BuildServices(DatabaseRole.Cli);
        await using AsyncServiceScope scope = provider.CreateAsyncScope();
        AppDbContext db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        string? schema = await db.Database
            .SqlQuery<string>($"SELECT schemaname AS \"Value\" FROM pg_tables WHERE tablename = '__ef_migrations_history'")
            .SingleOrDefaultAsync(Token);
        string? owner = await db.Database
            .SqlQuery<string>($"SELECT tableowner AS \"Value\" FROM pg_tables WHERE tablename = '__ef_migrations_history'")
            .SingleOrDefaultAsync(Token);

        Assert.Equal(DatabaseSchemas.Migrations, schema);
        Assert.Equal("owner", owner);
    }
}
