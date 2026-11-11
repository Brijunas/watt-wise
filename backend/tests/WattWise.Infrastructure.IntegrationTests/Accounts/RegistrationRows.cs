using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

using WattWise.Infrastructure.Persistence;

namespace WattWise.Infrastructure.IntegrationTests.Accounts;

/// <summary>Reads what a registration stored, through the <c>cli</c> role (the <c>api</c> role may only insert outbox rows), in a new scope.</summary>
internal static class RegistrationRows
{
    /// <summary>The number of account, AccountIdentity and email request rows of an account, as <c>accounts|identities|requests</c>.</summary>
    public static async Task<string> OfAccountAsync(ServiceProvider reader, Guid accountId)
    {
        await using AsyncServiceScope scope = reader.CreateAsyncScope();
        AppDbContext db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        return await db.Database
            .SqlQuery<string>($"""
                SELECT (SELECT count(*) FROM app.accounts WHERE id = {accountId})::text
                    || '|' || (SELECT count(*) FROM app.identity_users WHERE account_id = {accountId})::text
                    || '|' || (SELECT count(*) FROM app.email_requests WHERE account_id = {accountId})::text AS "Value"
                """)
            .SingleAsync(AccountsTestSupport.Token);
    }
}
