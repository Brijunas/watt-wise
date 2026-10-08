using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

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
}
