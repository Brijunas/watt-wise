using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

using WattWise.Domain.Accounts;
using WattWise.Infrastructure.Persistence;

namespace WattWise.Infrastructure.IntegrationTests.Accounts;

public class RolesAndPermissionsTests(DatabaseFixture fixture) : IClassFixture<DatabaseFixture>
{
    private static CancellationToken Token => AccountsTestSupport.Token;

    // characterization: RoleConfiguration.cs:18-21, AccountRoleConfiguration.cs:14-17 (deleting a role removes its links only)
    [Fact]
    public async Task Deleting_a_role_removes_its_links_but_not_the_accounts_or_permissions()
    {
        await using ServiceProvider provider = fixture.BuildServices(DatabaseRole.Api);
        Permission permission = NewPermission();
        Role role = Role.Create(AccountsTestSupport.UniqueName("role"));
        role.Grant(permission.Id);
        Account account = AccountsTestSupport.NewAccount();
        account.AddRole(role.Id);
        await using (AsyncServiceScope seedScope = provider.CreateAsyncScope())
        {
            AppDbContext seedDb = seedScope.ServiceProvider.GetRequiredService<AppDbContext>();
            seedDb.Permissions.Add(permission);
            seedDb.Roles.Add(role);
            seedDb.Accounts.Add(account);
            await seedDb.SaveChangesAsync(Token);
        }

        Assert.Equal((1L, 1L, 1L, 1L), await CountsAsync(provider, account.Id, role.Id, permission.Id));

        await using (AsyncServiceScope deleteScope = provider.CreateAsyncScope())
        {
            AppDbContext deleteDb = deleteScope.ServiceProvider.GetRequiredService<AppDbContext>();
            await deleteDb.Roles.Where(r => r.Id == role.Id).ExecuteDeleteAsync(Token);
        }

        // account links, permission links, permissions, accounts
        Assert.Equal((0L, 0L, 1L, 1L), await CountsAsync(provider, account.Id, role.Id, permission.Id));
    }

    // characterization: RolePermissionConfiguration.cs:14-17 (deleting a permission removes it from every role)
    [Fact]
    public async Task Deleting_a_permission_takes_it_away_from_every_role()
    {
        await using ServiceProvider provider = fixture.BuildServices(DatabaseRole.Api);
        Permission permission = NewPermission();
        Role first = Role.Create(AccountsTestSupport.UniqueName("role"));
        first.Grant(permission.Id);
        Role second = Role.Create(AccountsTestSupport.UniqueName("role"));
        second.Grant(permission.Id);
        await using (AsyncServiceScope seedScope = provider.CreateAsyncScope())
        {
            AppDbContext seedDb = seedScope.ServiceProvider.GetRequiredService<AppDbContext>();
            seedDb.Permissions.Add(permission);
            seedDb.Roles.AddRange(first, second);
            await seedDb.SaveChangesAsync(Token);
        }

        Assert.Equal((2L, 2L), await PermissionCountsAsync(provider, permission.Id, first.Id, second.Id));

        await using (AsyncServiceScope deleteScope = provider.CreateAsyncScope())
        {
            AppDbContext deleteDb = deleteScope.ServiceProvider.GetRequiredService<AppDbContext>();
            await deleteDb.Permissions.Where(p => p.Id == permission.Id).ExecuteDeleteAsync(Token);
        }

        // links of the permission, roles
        Assert.Equal((0L, 2L), await PermissionCountsAsync(provider, permission.Id, first.Id, second.Id));
    }

    [Fact]
    public async Task A_duplicate_role_name_is_rejected()
    {
        await using ServiceProvider provider = fixture.BuildServices(DatabaseRole.Api);
        Role first = Role.Create(AccountsTestSupport.UniqueName("role"));

        DbUpdateException exception = await SaveSecondAsync(provider, first, Role.Create(first.Name));

        Assert.Equal("23505:ix_roles_name", AccountsTestSupport.ViolationOf(exception));
    }

    [Fact]
    public async Task A_duplicate_permission_name_is_rejected()
    {
        await using ServiceProvider provider = fixture.BuildServices(DatabaseRole.Api);
        Permission first = NewPermission();

        DbUpdateException exception = await SaveSecondAsync(provider, first, Permission.Create(first.Name, "again"));

        Assert.Equal("23505:ix_permissions_name", AccountsTestSupport.ViolationOf(exception));
    }

    // characterization: AccountRoleConfiguration.cs:13 (the primary key holds an account's role once).
    // Both contexts load the account without the role and add it; the barrier holds each SaveChanges until both
    // have arrived, so both insert and the second hits the primary key.
    [Fact]
    public async Task Two_contexts_adding_the_same_role_at_once_let_one_win_and_the_other_fail_on_the_primary_key()
    {
        SaveChangesBarrier barrier = new();
        await using ServiceProvider provider = fixture.BuildAccountServices(
            configure: services => services.AddDbContext<AppDbContext>(options => options.AddInterceptors(barrier)));
        Role role = Role.Create(AccountsTestSupport.UniqueName("role"));
        Account account = AccountsTestSupport.NewAccount();
        await using (AsyncServiceScope seedScope = provider.CreateAsyncScope())
        {
            AppDbContext seedDb = seedScope.ServiceProvider.GetRequiredService<AppDbContext>();
            seedDb.Roles.Add(role);
            seedDb.Accounts.Add(account);
            await seedDb.SaveChangesAsync(Token);
        }

        await using AsyncServiceScope firstScope = provider.CreateAsyncScope();
        AppDbContext firstDb = firstScope.ServiceProvider.GetRequiredService<AppDbContext>();
        Account firstAccount = await firstDb.Accounts.SingleAsync(a => a.Id == account.Id, Token);
        await using AsyncServiceScope secondScope = provider.CreateAsyncScope();
        AppDbContext secondDb = secondScope.ServiceProvider.GetRequiredService<AppDbContext>();
        Account secondAccount = await secondDb.Accounts.SingleAsync(a => a.Id == account.Id, Token);
        firstAccount.AddRole(role.Id);
        secondAccount.AddRole(role.Id);
        barrier.Arm(2);

        Exception?[] failures = await Task.WhenAll(
            Record.ExceptionAsync(() => firstDb.SaveChangesAsync(Token)).AsTask(),
            Record.ExceptionAsync(() => secondDb.SaveChangesAsync(Token)).AsTask());

        string outcomes = string.Join(
            ",", failures.Select(failure => failure is null ? "saved" : AccountsTestSupport.ViolationOf(failure)).Order());
        Assert.Equal("23505:pk_account_roles,saved", outcomes);
    }

    private static Permission NewPermission() =>
        Permission.Create(AccountsTestSupport.UniqueName("permission"), "A permission for a test.");

    // Saves the first in one scope, then the second, whose name collides, in another.
    private static async Task<DbUpdateException> SaveSecondAsync<T>(ServiceProvider provider, T first, T second)
        where T : class
    {
        await using (AsyncServiceScope firstScope = provider.CreateAsyncScope())
        {
            AppDbContext firstDb = firstScope.ServiceProvider.GetRequiredService<AppDbContext>();
            firstDb.Add(first);
            await firstDb.SaveChangesAsync(Token);
        }

        await using AsyncServiceScope secondScope = provider.CreateAsyncScope();
        AppDbContext secondDb = secondScope.ServiceProvider.GetRequiredService<AppDbContext>();
        secondDb.Add(second);
        return await Assert.ThrowsAsync<DbUpdateException>(() => secondDb.SaveChangesAsync(Token));
    }

    private static async Task<(long AccountLinks, long PermissionLinks, long Permissions, long Accounts)> CountsAsync(
        ServiceProvider provider, Guid accountId, Guid roleId, Guid permissionId) =>
        (
            await AccountsTestSupport.CountAsync(provider, $"SELECT count(*) AS \"Value\" FROM app.account_roles WHERE role_id = {roleId}", Token),
            await AccountsTestSupport.CountAsync(provider, $"SELECT count(*) AS \"Value\" FROM app.role_permissions WHERE role_id = {roleId}", Token),
            await AccountsTestSupport.CountAsync(provider, $"SELECT count(*) AS \"Value\" FROM app.permissions WHERE id = {permissionId}", Token),
            await AccountsTestSupport.CountAsync(provider, $"SELECT count(*) AS \"Value\" FROM app.accounts WHERE id = {accountId}", Token));

    private static async Task<(long Links, long Roles)> PermissionCountsAsync(
        ServiceProvider provider, Guid permissionId, Guid firstRoleId, Guid secondRoleId) =>
        (
            await AccountsTestSupport.CountAsync(provider, $"SELECT count(*) AS \"Value\" FROM app.role_permissions WHERE permission_id = {permissionId}", Token),
            await AccountsTestSupport.CountAsync(provider, $"SELECT count(*) AS \"Value\" FROM app.roles WHERE id IN ({firstRoleId}, {secondRoleId})", Token));
}
