using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

using Npgsql;

using WattWise.Domain.Accounts;
using WattWise.Infrastructure.Persistence;

namespace WattWise.Infrastructure.IntegrationTests;

public class RolesAndPermissionsTests(DatabaseFixture fixture) : IClassFixture<DatabaseFixture>
{
    private const string UniqueViolation = "23505";

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Permissions_shared_by_two_roles_are_listed_once()
    {
        Permission read = NewPermission();
        Permission write = NewPermission();
        Permission delete = NewPermission();
        Role editor = Role.Create(AccountsTestSupport.UniqueName("editor"));
        editor.Grant(read);
        editor.Grant(write);
        Role reviewer = Role.Create(AccountsTestSupport.UniqueName("reviewer"));
        reviewer.Grant(write);
        reviewer.Grant(delete);
        Account account = AccountsTestSupport.NewAccount();
        account.AddRole(editor);
        account.AddRole(reviewer);
        await using ServiceProvider provider = fixture.BuildServices(DatabaseRole.Api);
        await using AsyncServiceScope scope = provider.CreateAsyncScope();
        AppDbContext db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        db.Accounts.Add(account);
        await db.SaveChangesAsync(Token);

        await using ServiceProvider readerProvider = fixture.BuildServices(DatabaseRole.Api);
        await using AsyncServiceScope readerScope = readerProvider.CreateAsyncScope();
        AppDbContext readDb = readerScope.ServiceProvider.GetRequiredService<AppDbContext>();
        Account loaded = await readDb.Accounts
            .Include(a => a.Roles).ThenInclude(r => r.Permissions)
            .SingleAsync(a => a.Id == account.Id, Token);

        IReadOnlyCollection<string> permissions = loaded.Permissions();

        Assert.Equal(3, permissions.Count);
        Assert.Equivalent(new[] { read.Name, write.Name, delete.Name }, permissions, strict: false);
    }

    [Fact]
    public async Task Adding_a_role_the_account_already_has_through_another_context_keeps_one_link()
    {
        Role role = Role.Create(AccountsTestSupport.UniqueName("role"));
        Account account = AccountsTestSupport.NewAccount();
        account.AddRole(role);
        await using ServiceProvider provider = fixture.BuildServices(DatabaseRole.Api);
        await using (AsyncServiceScope seedScope = provider.CreateAsyncScope())
        {
            AppDbContext seedDb = seedScope.ServiceProvider.GetRequiredService<AppDbContext>();
            seedDb.Accounts.Add(account);
            await seedDb.SaveChangesAsync(Token);
        }

        await using AsyncServiceScope accountScope = provider.CreateAsyncScope();
        AppDbContext accountDb = accountScope.ServiceProvider.GetRequiredService<AppDbContext>();
        Account loaded = await accountDb.Accounts
            .Include(a => a.Roles)
            .SingleAsync(a => a.Id == account.Id, Token);
        await using AsyncServiceScope roleScope = provider.CreateAsyncScope();
        AppDbContext roleDb = roleScope.ServiceProvider.GetRequiredService<AppDbContext>();
        Role sameRole = await roleDb.Roles.SingleAsync(r => r.Id == role.Id, Token);
        Assert.NotSame(loaded.Roles.Single(), sameRole);

        loaded.AddRole(sameRole);
        await accountDb.SaveChangesAsync(Token);

        Assert.Equal(1, await CountAsync(accountDb, $"SELECT count(*) AS \"Value\" FROM app.account_roles WHERE account_id = {account.Id}"));
    }

    [Fact]
    public async Task A_duplicate_permission_name_is_rejected()
    {
        Permission first = NewPermission();
        await using ServiceProvider provider = fixture.BuildServices(DatabaseRole.Api);
        await using AsyncServiceScope scope = provider.CreateAsyncScope();
        AppDbContext db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        db.Permissions.Add(first);
        await db.SaveChangesAsync(Token);
        db.Permissions.Add(Permission.Create(first.Name, "again"));

        DbUpdateException exception = await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync(Token));

        PostgresException postgres = Assert.IsType<PostgresException>(exception.InnerException);
        Assert.Equal(UniqueViolation, postgres.SqlState);
        Assert.Equal("ix_permissions_name", postgres.ConstraintName);
    }

    [Fact]
    public async Task A_duplicate_role_name_is_rejected()
    {
        Role first = Role.Create(AccountsTestSupport.UniqueName("role"));
        await using ServiceProvider provider = fixture.BuildServices(DatabaseRole.Api);
        await using AsyncServiceScope scope = provider.CreateAsyncScope();
        AppDbContext db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        db.Roles.Add(first);
        await db.SaveChangesAsync(Token);
        db.Roles.Add(Role.Create(first.Name));

        DbUpdateException exception = await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync(Token));

        PostgresException postgres = Assert.IsType<PostgresException>(exception.InnerException);
        Assert.Equal(UniqueViolation, postgres.SqlState);
        Assert.Equal("ix_roles_name", postgres.ConstraintName);
    }

    [Fact]
    public async Task Deleting_a_role_removes_its_links_but_not_the_permissions()
    {
        Permission permission = NewPermission();
        Role role = Role.Create(AccountsTestSupport.UniqueName("role"));
        role.Grant(permission);
        Account account = AccountsTestSupport.NewAccount();
        account.AddRole(role);
        await using ServiceProvider provider = fixture.BuildServices(DatabaseRole.Api);
        await using AsyncServiceScope scope = provider.CreateAsyncScope();
        AppDbContext db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        db.Accounts.Add(account);
        await db.SaveChangesAsync(Token);

        await db.Roles.Where(r => r.Id == role.Id).ExecuteDeleteAsync(Token);

        Assert.Equal(0, await CountAsync(db, $"SELECT count(*) AS \"Value\" FROM app.account_roles WHERE role_id = {role.Id}"));
        Assert.Equal(0, await CountAsync(db, $"SELECT count(*) AS \"Value\" FROM app.role_permissions WHERE role_id = {role.Id}"));
        Assert.Equal(1, await CountAsync(db, $"SELECT count(*) AS \"Value\" FROM app.permissions WHERE id = {permission.Id}"));
        Assert.Equal(1, await CountAsync(db, $"SELECT count(*) AS \"Value\" FROM app.accounts WHERE id = {account.Id}"));
    }

    [Fact]
    public async Task Deleting_a_permission_takes_it_away_from_every_role()
    {
        Permission permission = NewPermission();
        Role first = Role.Create(AccountsTestSupport.UniqueName("role"));
        first.Grant(permission);
        Role second = Role.Create(AccountsTestSupport.UniqueName("role"));
        second.Grant(permission);
        await using ServiceProvider provider = fixture.BuildServices(DatabaseRole.Api);
        await using AsyncServiceScope scope = provider.CreateAsyncScope();
        AppDbContext db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        db.Roles.AddRange(first, second);
        await db.SaveChangesAsync(Token);
        Assert.Equal(2, await CountAsync(db, $"SELECT count(*) AS \"Value\" FROM app.role_permissions WHERE permission_id = {permission.Id}"));

        await db.Permissions.Where(p => p.Id == permission.Id).ExecuteDeleteAsync(Token);

        Assert.Equal(0, await CountAsync(db, $"SELECT count(*) AS \"Value\" FROM app.role_permissions WHERE permission_id = {permission.Id}"));
        Assert.Equal(2, await CountAsync(db, $"SELECT count(*) AS \"Value\" FROM app.roles WHERE id IN ({first.Id}, {second.Id})"));
    }

    private static Permission NewPermission() =>
        Permission.Create(AccountsTestSupport.UniqueName("permission"), "A permission for a test.");

    private static Task<long> CountAsync(AppDbContext db, FormattableString sql) =>
        db.Database.SqlQuery<long>(sql).SingleAsync(Token);
}
