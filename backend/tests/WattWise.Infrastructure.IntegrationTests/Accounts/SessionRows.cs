using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

using WattWise.Infrastructure.Persistence;

namespace WattWise.Infrastructure.IntegrationTests.Accounts;

/// <summary>Reads what sessions stored, through the <c>cli</c> role, in a new scope.</summary>
internal static class SessionRows
{
    /// <summary>The number of session rows and of refresh token rows of a session, as <c>sessions|tokens</c>.</summary>
    public static async Task<string> OfSessionAsync(ServiceProvider reader, Guid sessionId)
    {
        await using AsyncServiceScope scope = reader.CreateAsyncScope();
        AppDbContext db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        return await db.Database
            .SqlQuery<string>($"""
                SELECT (SELECT count(*) FROM app.sessions WHERE id = {sessionId})::text
                    || '|' || (SELECT count(*) FROM app.refresh_tokens WHERE session_id = {sessionId})::text AS "Value"
                """)
            .SingleAsync(AccountsTestSupport.Token);
    }

    /// <summary>The hex token hashes still stored for a session, sorted.</summary>
    public static async Task<string[]> TokenHashesAsync(ServiceProvider reader, Guid sessionId)
    {
        await using AsyncServiceScope scope = reader.CreateAsyncScope();
        AppDbContext db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        return await db.Database
            .SqlQuery<string>($"""SELECT upper(encode(token_hash, 'hex')) AS "Value" FROM app.refresh_tokens WHERE session_id = {sessionId} ORDER BY 1""")
            .ToArrayAsync(AccountsTestSupport.Token);
    }
}
