using System.Diagnostics.CodeAnalysis;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

using WattWise.Infrastructure.Persistence;

namespace WattWise.Infrastructure.IntegrationTests;

/// <summary>What the platform tests share: raw SQL as a given role in a scope of its own, and the probe table each schema gets.</summary>
[SuppressMessage("Security", "EF1003:Possible SQL injection", Justification = "Schema names can't be parameters; the tests pass literals, values are parameters.")]
internal static class PlatformSql
{
    public static CancellationToken Token => TestContext.Current.CancellationToken;

    public static string ProbeTable(string schema) => schema + ".access_probe";

    /// <summary>Runs <paramref name="sql"/> as <paramref name="role"/> in a scope of its own and returns the rows affected.</summary>
    public static async Task<int> ExecuteAsync(DatabaseFixture fixture, DatabaseRole role, string sql, params object[] parameters)
    {
        await using ServiceProvider provider = fixture.BuildServices(role);
        await using AsyncServiceScope scope = provider.CreateAsyncScope();
        AppDbContext db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        return await db.Database.ExecuteSqlRawAsync(sql, parameters, Token);
    }

    /// <summary>Runs a query that returns one column called <c>Value</c> as <paramref name="role"/> in a scope of its own.</summary>
    public static async Task<List<T>> QueryAsync<T>(DatabaseFixture fixture, DatabaseRole role, string sql, params object[] parameters)
    {
        await using ServiceProvider provider = fixture.BuildServices(role);
        await using AsyncServiceScope scope = provider.CreateAsyncScope();
        AppDbContext db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        return await db.Database.SqlQueryRaw<T>(sql, parameters).ToListAsync(Token);
    }

    /// <summary>
    /// Creates the probe table in <paramref name="schema"/> as the Cli (acting as owner, so the default privileges apply).
    /// The key is <c>serial</c>, not an identity column: only a <c>serial</c> default calls <c>nextval</c>, which needs the sequence grants.
    /// </summary>
    public static Task CreateProbeTableAsync(DatabaseFixture fixture, string schema) =>
        ExecuteAsync(
            fixture,
            DatabaseRole.Cli,
            "CREATE TABLE IF NOT EXISTS " + ProbeTable(schema) + " (id serial PRIMARY KEY, note text)");

    /// <summary>Inserts a row with a fresh note as the Cli and returns the note.</summary>
    public static async Task<string> SeedProbeRowAsync(DatabaseFixture fixture, string schema)
    {
        string note = "probe-" + Guid.NewGuid().ToString("N");
        await ExecuteAsync(fixture, DatabaseRole.Cli, "INSERT INTO " + ProbeTable(schema) + " (note) VALUES ({0})", note);
        return note;
    }
}
