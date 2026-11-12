using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

using WattWise.Application.Accounts;
using WattWise.Infrastructure.Persistence;

namespace WattWise.Cli.Tests;

/// <summary>Reads what a CLI run left in the database, through the <c>cli</c> role's services in a scope of its own.</summary>
internal static class StoredAccounts
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    public static string UniqueEmail() => $"smoke-{Guid.NewGuid():N}@example.com";

    /// <summary>A password that follows every rule.</summary>
    public static string ValidPassword() => $"Aa1!{Guid.NewGuid():N}";

    public static async Task<long> CountAsync(ServiceProvider provider, string email)
    {
        await using AsyncServiceScope scope = provider.CreateAsyncScope();
        AppDbContext db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        return await db.Database
            .SqlQuery<long>($"""SELECT count(*) AS "Value" FROM app.identity_users WHERE upper(email) = {email.ToUpperInvariant()}""")
            .SingleAsync(Token);
    }

    /// <summary>The ids of the roles the account with the email holds, sorted, as text.</summary>
    public static async Task<string> RoleIdsAsync(ServiceProvider provider, string email)
    {
        await using AsyncServiceScope scope = provider.CreateAsyncScope();
        AppDbContext db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        string[] ids = await db.Database
            .SqlQuery<string>($"""
                SELECT r.role_id::text AS "Value" FROM app.account_roles r
                JOIN app.identity_users i ON i.account_id = r.account_id
                WHERE upper(i.email) = {email.ToUpperInvariant()} ORDER BY 1
                """)
            .ToArrayAsync(Token);
        return string.Join(",", ids);
    }

    public static async Task<PasswordCheckOutcome> SignInAsync(ServiceProvider provider, string email, string password)
    {
        await using AsyncServiceScope scope = provider.CreateAsyncScope();
        PasswordCheck check = await scope.ServiceProvider.GetRequiredService<IPasswordSignIn>().CheckAsync(email, password, Token);
        return check.Outcome;
    }
}
