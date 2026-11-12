using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

using WattWise.Infrastructure.Persistence;

namespace WattWise.Infrastructure.IntegrationTests.Accounts;

public class SeededAdminRoleTests(DatabaseFixture fixture) : IClassFixture<DatabaseFixture>
{
    [Fact]
    public async Task The_migrated_database_has_an_Admin_role_that_grants_exactly_admin_access()
    {
        await using ServiceProvider reader = fixture.BuildServices(DatabaseRole.Cli);
        await using AsyncServiceScope scope = reader.CreateAsyncScope();
        AppDbContext db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        string[] rows = await db.Database.SqlQuery<string>($"""
            SELECT r.id::text || '|' || r.name || '|' || p.id::text || '|' || p.name AS "Value"
            FROM app.roles r
            JOIN app.role_permissions rp ON rp.role_id = r.id
            JOIN app.permissions p ON p.id = rp.permission_id
            WHERE r.name = 'Admin'
            """).ToArrayAsync(AccountsTestSupport.Token);

        Assert.Equal(
            ["0192e5a0-0000-7000-8000-000000000001|Admin|0192e5a0-0000-7000-8000-000000000002|admin.access"],
            rows);
    }
}
