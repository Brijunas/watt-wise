using Microsoft.EntityFrameworkCore;

using WattWise.Application.Accounts;
using WattWise.Domain.Accounts;
using WattWise.Domain.Localization;

namespace WattWise.Infrastructure.Persistence.Queries;

internal sealed class AccountsQueries(AppDbContext context) : IAccountsQueries
{
    public async Task<IReadOnlyList<string>> GetPermissionsAsync(Guid accountId, CancellationToken cancellationToken)
    {
        List<string> names = await (
            from accountRole in context.Set<AccountRole>().AsNoTracking()
            where accountRole.AccountId == accountId
            join rolePermission in context.Set<RolePermission>() on accountRole.RoleId equals rolePermission.RoleId
            join permission in context.Permissions on rolePermission.PermissionId equals permission.Id
            select permission.Name)
            .Distinct()
            .ToListAsync(cancellationToken);

        // Sorted here and not in SQL, so the order doesn't depend on the database's collation.
        names.Sort(StringComparer.Ordinal);
        return names;
    }

    public async Task<string?> GetSecurityStampAsync(Guid accountId, CancellationToken cancellationToken)
    {
        return await context.Users.AsNoTracking()
            .Where(identity => identity.AccountId == accountId)
            .Select(identity => identity.SecurityStamp)
            .SingleOrDefaultAsync(cancellationToken);
    }

    public async Task<Me?> GetMeAsync(Guid accountId, CancellationToken cancellationToken)
    {
        var account = await (
            from candidate in context.Accounts.AsNoTracking()
            where candidate.Id == accountId
            join identity in context.Users on candidate.Id equals identity.AccountId
            select new { identity.Email, candidate.PreferredLanguage })
            .SingleOrDefaultAsync(cancellationToken);
        if (account is null)
        {
            return null;
        }

        List<string> roles = await (
            from accountRole in context.Set<AccountRole>().AsNoTracking()
            where accountRole.AccountId == accountId
            join role in context.Roles on accountRole.RoleId equals role.Id
            select role.Name)
            .ToListAsync(cancellationToken);
        roles.Sort(StringComparer.Ordinal);

        IReadOnlyList<string> permissions = await GetPermissionsAsync(accountId, cancellationToken);
        return new Me(account.Email!, LanguageText(account.PreferredLanguage), roles, permissions);
    }

    private static string LanguageText(Language language)
    {
        return language switch
        {
            Language.Lt => "lt",
            Language.En => "en",
        };
    }
}
