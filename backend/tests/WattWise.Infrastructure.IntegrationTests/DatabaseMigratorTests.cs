using System.Collections.Concurrent;
using System.Diagnostics;

using Microsoft.Extensions.DependencyInjection;

using WattWise.Infrastructure.Persistence.Schema;

namespace WattWise.Infrastructure.IntegrationTests;

public class DatabaseMigratorTests(DatabaseFixture fixture) : IClassFixture<DatabaseFixture>
{
    private const string HistoryQuery =
        "SELECT migration_id || '/' || product_version AS \"Value\" FROM migrations.__ef_migrations_history ORDER BY migration_id";

    private static CancellationToken Token => PlatformSql.Token;

    // characterization: DatabaseMigrator.cs:22 (span name), EfCoreMigrationsStep.cs:9 and HangfireStorageStep.cs:18 (step names); the order is architecture.md "Persistence" (EF Core first, then Hangfire).
    [Fact]
    public async Task Each_schema_step_runs_in_its_own_span_with_ef_core_first()
    {
        using ActivitySource testSource = new("WattWise.Tests.Migrator");
        ConcurrentQueue<Activity> stopped = new();
        using ActivityListener listener = new()
        {
            ShouldListenTo = source => source.Name is "WattWise.Infrastructure" or "WattWise.Tests.Migrator",
            Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllDataAndRecorded,
            ActivityStopped = stopped.Enqueue,
        };
        ActivitySource.AddActivityListener(listener);
        await using ServiceProvider provider = fixture.BuildServices(DatabaseRole.Cli);
        await using AsyncServiceScope scope = provider.CreateAsyncScope();
        using Activity root = testSource.StartActivity("migrate") ?? throw new InvalidOperationException("The listener did not record the root span.");

        await scope.ServiceProvider.GetRequiredService<DatabaseMigrator>().MigrateAsync(Token);

        // Listeners are process-wide, so keep only this test's trace.
        string[] names = [.. stopped.Where(span => span.TraceId == root.TraceId && span.Parent == root).Select(span => span.DisplayName)];
        Assert.Equal(["schema-step ef-core-migrations", "schema-step hangfire-storage"], names);
    }

    [Fact]
    public async Task A_rerun_leaves_the_migration_history_unchanged()
    {
        List<string> before = await PlatformSql.QueryAsync<string>(fixture, DatabaseRole.Cli, HistoryQuery);
        Assert.NotEmpty(before);
        await using (ServiceProvider provider = fixture.BuildServices(DatabaseRole.Cli))
        await using (AsyncServiceScope scope = provider.CreateAsyncScope())
        {
            await scope.ServiceProvider.GetRequiredService<DatabaseMigrator>().MigrateAsync(Token);
        }

        List<string> after = await PlatformSql.QueryAsync<string>(fixture, DatabaseRole.Cli, HistoryQuery);

        Assert.Equal(before, after);
    }
}
