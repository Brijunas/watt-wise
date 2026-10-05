using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

using WattWise.Infrastructure.Persistence;

namespace WattWise.Jobs.IntegrationTests;

public class HangfireRoleTests(DatabaseFixture fixture) : IClassFixture<DatabaseFixture>
{
    [Fact]
    public async Task Jobs_connect_as_the_hangfire_role()
    {
        await using ServiceProvider provider = fixture.BuildServices(DatabaseRole.Hangfire);
        await using AsyncServiceScope scope = provider.CreateAsyncScope();
        AppDbContext db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        string user = await db.Database.SqlQueryRaw<string>("SELECT current_user AS \"Value\"")
            .SingleAsync(TestContext.Current.CancellationToken);

        Assert.Equal("hangfire", user);
    }
}
