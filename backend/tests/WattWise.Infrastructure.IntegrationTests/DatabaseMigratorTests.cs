using System.Collections.Concurrent;
using System.Data;
using System.Diagnostics;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

using Npgsql;

using WattWise.Infrastructure.Persistence;
using WattWise.Infrastructure.Persistence.Schema;

namespace WattWise.Infrastructure.IntegrationTests;

public class DatabaseMigratorTests(DatabaseFixture fixture) : IClassFixture<DatabaseFixture>
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    // characterization: HangfireStorageStep.cs:18 (the step name "hangfire-storage")
    private static ISchemaStep HangfireStep(AsyncServiceScope scope) =>
        scope.ServiceProvider.GetServices<ISchemaStep>().Single(step => step.Name == "hangfire-storage");

    private static NpgsqlConnection Connection(AsyncServiceScope scope) =>
        (NpgsqlConnection)scope.ServiceProvider.GetRequiredService<AppDbContext>().Database.GetDbConnection();

    [Fact]
    public async Task Migrating_an_up_to_date_database_leaves_the_migration_history_unchanged()
    {
        await using ServiceProvider provider = fixture.BuildServices(DatabaseRole.Cli);
        string[] before = await ReadHistoryAsync(provider);
        Assert.NotEmpty(before);
        await using (AsyncServiceScope scope = provider.CreateAsyncScope())
        {
            await scope.ServiceProvider.GetRequiredService<DatabaseMigrator>().MigrateAsync(Token);
        }

        string[] after = await ReadHistoryAsync(provider);

        Assert.Equal(before, after);
    }

    // characterization: DatabaseMigrator.cs:22, EfCoreMigrationsStep.cs:9, HangfireStorageStep.cs:18 (span name "schema-step <step name>")
    [Fact]
    public async Task The_real_migrator_runs_ef_core_before_hangfire_each_in_its_own_span()
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
        using Activity? root = testSource.StartActivity("migrate");
        Assert.NotNull(root);

        await scope.ServiceProvider.GetRequiredService<DatabaseMigrator>().MigrateAsync(Token);

        // Listeners are process-wide, so keep only this test's trace.
        string[] names = [.. stopped.Where(span => span.TraceId == root.TraceId).Select(span => span.DisplayName)];
        Assert.Equal(["schema-step ef-core-migrations", "schema-step hangfire-storage"], names);
    }

    [Fact]
    public async Task Hangfire_step_closes_the_connection_it_opened()
    {
        await using ServiceProvider provider = fixture.BuildServices(DatabaseRole.Cli);
        await using AsyncServiceScope scope = provider.CreateAsyncScope();
        NpgsqlConnection connection = Connection(scope);
        Assert.Equal(ConnectionState.Closed, connection.State);

        await HangfireStep(scope).ApplyAsync(Token);

        Assert.Equal(ConnectionState.Closed, connection.State);
    }

    [Fact]
    public async Task Hangfire_step_leaves_a_connection_open_that_the_caller_opened()
    {
        await using ServiceProvider provider = fixture.BuildServices(DatabaseRole.Cli);
        await using AsyncServiceScope scope = provider.CreateAsyncScope();
        NpgsqlConnection connection = Connection(scope);
        await connection.OpenAsync(Token);

        await HangfireStep(scope).ApplyAsync(Token);

        Assert.Equal(ConnectionState.Open, connection.State);
    }

    [Fact]
    public async Task Hangfire_step_stops_on_a_cancelled_token()
    {
        await using ServiceProvider provider = fixture.BuildServices(DatabaseRole.Cli);
        await using AsyncServiceScope scope = provider.CreateAsyncScope();
        await Connection(scope).OpenAsync(Token);
        using CancellationTokenSource cts = new();
        await cts.CancelAsync();

        await Assert.ThrowsAsync<OperationCanceledException>(() => HangfireStep(scope).ApplyAsync(cts.Token));
    }

    // Source: architecture.md:131 ("no command timeout"). Kills the removed SetCommandTimeout(0) in EfCoreMigrationsStep.cs:15.
    [Fact]
    public async Task Ef_core_step_clears_the_command_timeout()
    {
        await using ServiceProvider provider = fixture.BuildServices(DatabaseRole.Cli);
        await using AsyncServiceScope scope = provider.CreateAsyncScope();
        ISchemaStep step = scope.ServiceProvider.GetServices<ISchemaStep>().Single(s => s.Name == "ef-core-migrations");
        AppDbContext db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        await step.ApplyAsync(Token);

        Assert.Equal(0, db.Database.GetCommandTimeout());
    }

    private static async Task<string[]> ReadHistoryAsync(ServiceProvider provider)
    {
        await using AsyncServiceScope scope = provider.CreateAsyncScope();
        AppDbContext db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        return await db.Database
            .SqlQuery<string>($"SELECT migration_id || '|' || product_version AS \"Value\" FROM migrations.__ef_migrations_history ORDER BY migration_id")
            .ToArrayAsync(Token);
    }
}
