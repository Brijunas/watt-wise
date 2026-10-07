using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

using Npgsql;

using WattWise.Infrastructure.Persistence;
using WattWise.Infrastructure.Persistence.Schema;

namespace WattWise.Infrastructure.IntegrationTests;

public class HangfireSchemaTests(DatabaseFixture fixture) : IClassFixture<DatabaseFixture>
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact]
    public async Task The_hangfire_tables_exist_and_belong_to_the_owner()
    {
        await using ServiceProvider provider = fixture.BuildServices(DatabaseRole.Cli);
        await using AsyncServiceScope scope = provider.CreateAsyncScope();
        AppDbContext db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        string? owner = await db.Database
            .SqlQuery<string>($"SELECT tableowner AS \"Value\" FROM pg_tables WHERE schemaname = {DatabaseSchemas.Hangfire} AND tablename = 'job'")
            .SingleOrDefaultAsync(Token);

        Assert.Equal("owner", owner);
    }

    [Fact]
    public async Task Hangfire_can_write_to_its_tables()
    {
        await using ServiceProvider provider = fixture.BuildServices(DatabaseRole.Hangfire);
        await using AsyncServiceScope scope = provider.CreateAsyncScope();
        AppDbContext db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        int inserted = await db.Database.ExecuteSqlRawAsync(
            "INSERT INTO hangfire.counter (key, value) VALUES ('schema-test', 1)", Token);
        int deleted = await db.Database.ExecuteSqlRawAsync(
            "DELETE FROM hangfire.counter WHERE key = 'schema-test'", Token);

        Assert.Equal(1, inserted);
        Assert.Equal(1, deleted);
    }

    [Fact]
    public async Task Api_cannot_read_the_hangfire_tables()
    {
        await using ServiceProvider provider = fixture.BuildServices(DatabaseRole.Api);
        await using AsyncServiceScope scope = provider.CreateAsyncScope();
        AppDbContext db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        PostgresException exception = await Assert.ThrowsAsync<PostgresException>(
            () => db.Database.SqlQueryRaw<long>("SELECT count(*) AS \"Value\" FROM hangfire.job").ToListAsync(Token));

        Assert.Equal("42501", exception.SqlState);
    }
}
