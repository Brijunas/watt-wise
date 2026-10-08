using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

using WattWise.Infrastructure.Persistence;

namespace WattWise.Jobs.IntegrationTests;

public class JobsRoleTests(DatabaseFixture fixture) : IClassFixture<DatabaseFixture>
{
    [Fact]
    public async Task Jobs_connect_as_the_jobs_role()
    {
        await using ServiceProvider provider = fixture.BuildServices(DatabaseRole.Jobs);
        await using AsyncServiceScope scope = provider.CreateAsyncScope();
        AppDbContext db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        string user = await db.Database.SqlQueryRaw<string>("SELECT current_user AS \"Value\"")
            .SingleAsync(TestContext.Current.CancellationToken);

        Assert.Equal("jobs", user);
    }
}
