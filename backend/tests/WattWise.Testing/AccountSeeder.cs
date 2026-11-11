using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

using NodaTime;

using WattWise.Domain.Accounts;
using WattWise.Domain.Localization;
using WattWise.Infrastructure.Accounts;
using WattWise.Infrastructure.Persistence;

namespace WattWise.Testing;

/// <summary>
/// Seeds accounts, roles and permissions through the <c>api</c> role's services, each step in a scope of its own, the way
/// requests run. Names must be unique in the database (use a <see cref="Guid"/> in them): tests in a class share one clone.
/// </summary>
public static class AccountSeeder
{
    /// <summary>The ids of an account and of its AccountIdentity.</summary>
    public sealed record Ids(Guid AccountId, Guid IdentityId);

    /// <summary>An account with an AccountIdentity that has <paramref name="password"/> (none when null); the email is confirmed unless <paramref name="confirmed"/> is false.</summary>
    public static async Task<Ids> CreateAccountAsync(
        IServiceProvider provider, string email, string? password, bool confirmed = true, Language language = Language.Lt)
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        await using AsyncServiceScope scope = provider.CreateAsyncScope();
        AppDbContext db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        Account account = Account.Create(language, SystemClock.Instance.GetCurrentInstant());
        db.Accounts.Add(account);
        await db.SaveChangesAsync(cancellationToken);

        UserManager<AccountIdentity> users = scope.ServiceProvider.GetRequiredService<UserManager<AccountIdentity>>();
        AccountIdentity identity = AccountIdentity.Create(account.Id, email);
        identity.EmailConfirmed = confirmed;
        IdentityResult result = password is null ? await users.CreateAsync(identity) : await users.CreateAsync(identity, password);
        Assert.True(result.Succeeded, string.Join(", ", result.Errors.Select(error => error.Code)));
        return new Ids(account.Id, identity.Id);
    }

    public static async Task<Guid> CreatePermissionAsync(IServiceProvider provider, string name)
    {
        await using AsyncServiceScope scope = provider.CreateAsyncScope();
        AppDbContext db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        Permission permission = Permission.Create(name, "Test permission.");
        db.Permissions.Add(permission);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        return permission.Id;
    }

    public static async Task<Guid> CreateRoleAsync(IServiceProvider provider, string name, params Guid[] permissionIds)
    {
        await using AsyncServiceScope scope = provider.CreateAsyncScope();
        AppDbContext db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        Role role = Role.Create(name);
        foreach (Guid permissionId in permissionIds)
        {
            role.Grant(permissionId);
        }

        db.Roles.Add(role);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        return role.Id;
    }

    public static async Task AssignRoleAsync(IServiceProvider provider, Guid accountId, Guid roleId)
    {
        await using AsyncServiceScope scope = provider.CreateAsyncScope();
        AppDbContext db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        Account account = await db.Accounts.SingleAsync(candidate => candidate.Id == accountId, TestContext.Current.CancellationToken);
        account.AddRole(roleId);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    /// <summary>Creates a role named <paramref name="roleName"/> with a new permission per name and gives it to the account.</summary>
    public static async Task GrantAsync(IServiceProvider provider, Guid accountId, string roleName, params string[] permissionNames)
    {
        List<Guid> permissionIds = [];
        foreach (string name in permissionNames)
        {
            permissionIds.Add(await CreatePermissionAsync(provider, name));
        }

        Guid roleId = await CreateRoleAsync(provider, roleName, [.. permissionIds]);
        await AssignRoleAsync(provider, accountId, roleId);
    }
}
