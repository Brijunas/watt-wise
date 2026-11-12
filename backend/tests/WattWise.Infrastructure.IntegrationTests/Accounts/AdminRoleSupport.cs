using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

using WattWise.Application;
using WattWise.Application.Accounts;
using WattWise.Application.Results;
using WattWise.Infrastructure.Persistence;

namespace WattWise.Infrastructure.IntegrationTests.Accounts;

/// <summary>
/// Runs the grant and revoke handlers the way <c>WattWise.Cli admin</c> does: on the <c>cli</c> role's services (which act as
/// <c>owner</c>), each call in a scope of its own, and reads <c>account_roles</c> back through another scope.
/// </summary>
internal static class AdminRoleSupport
{
    public static async Task<Result<RoleChange>> GrantAsync(ServiceProvider cli, string? email)
    {
        await using AsyncServiceScope scope = cli.CreateAsyncScope();
        GrantAdminRoleCommandHandler handler = new(
            scope.ServiceProvider.GetRequiredService<IEmailAddressNormalizer>(),
            scope.ServiceProvider.GetRequiredService<IAccountRoles>());
        return await handler.Handle(new GrantAdminRoleCommand(email), AccountsTestSupport.Token);
    }

    public static async Task<Result<RoleChange>> RevokeAsync(ServiceProvider cli, string? email)
    {
        await using AsyncServiceScope scope = cli.CreateAsyncScope();
        RevokeAdminRoleCommandHandler handler = new(
            scope.ServiceProvider.GetRequiredService<IEmailAddressNormalizer>(),
            scope.ServiceProvider.GetRequiredService<IAccountRoles>(),
            scope.ServiceProvider.GetRequiredService<IAccountSessions>(),
            scope.ServiceProvider.GetRequiredService<ITransactionRunner>());
        return await handler.Handle(new RevokeAdminRoleCommand(email), AccountsTestSupport.Token);
    }

    /// <summary>The ids of the roles the account holds, sorted, as text.</summary>
    public static async Task<string> RoleIdsOfAsync(ServiceProvider reader, Guid accountId)
    {
        await using AsyncServiceScope scope = reader.CreateAsyncScope();
        AppDbContext db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        string[] ids = await db.Database
            .SqlQuery<string>($"""SELECT role_id::text AS "Value" FROM app.account_roles WHERE account_id = {accountId} ORDER BY 1""")
            .ToArrayAsync(AccountsTestSupport.Token);
        return string.Join(",", ids);
    }
}
