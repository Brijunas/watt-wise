using Microsoft.EntityFrameworkCore;

using WattWise.Application.Accounts;
using WattWise.Domain.Accounts;
using WattWise.Infrastructure.Persistence;

namespace WattWise.Infrastructure.Accounts;

/// <summary>Writes <c>account_roles</c> with one statement per change, so a repeated grant or a concurrent one cannot fail on the key.</summary>
internal sealed class AccountRoles(AppDbContext context) : IAccountRoles
{
    public async Task<RoleHolder?> FindByEmailAsync(string normalizedEmail, CancellationToken cancellationToken)
    {
        return await context.Users.AsNoTracking()
            .Where(identity => identity.NormalizedEmail == normalizedEmail)
            .Select(identity => new RoleHolder(identity.AccountId, identity.EmailConfirmed))
            .SingleOrDefaultAsync(cancellationToken);
    }

    public async Task<bool> AddAsync(Guid accountId, string roleName, CancellationToken cancellationToken)
    {
        Guid roleId = await FindRoleIdAsync(roleName, cancellationToken);
        int rows = await context.Database.ExecuteSqlAsync(
            $"INSERT INTO app.account_roles (account_id, role_id) VALUES ({accountId}, {roleId}) ON CONFLICT DO NOTHING",
            cancellationToken);
        return rows > 0;
    }

    public async Task<bool> RemoveAsync(Guid accountId, string roleName, CancellationToken cancellationToken)
    {
        Guid roleId = await FindRoleIdAsync(roleName, cancellationToken);
        int rows = await context.Set<AccountRole>()
            .Where(link => link.AccountId == accountId && link.RoleId == roleId)
            .ExecuteDeleteAsync(cancellationToken);
        return rows > 0;
    }

    // A role the migrations seed; a missing one is a broken database and throws.
    private async Task<Guid> FindRoleIdAsync(string roleName, CancellationToken cancellationToken)
    {
        return await context.Roles.AsNoTracking()
            .Where(role => role.Name == roleName)
            .Select(role => role.Id)
            .SingleAsync(cancellationToken);
    }
}
