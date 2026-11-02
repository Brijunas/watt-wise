using System.Data;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

using Npgsql;

using WattWise.Infrastructure.Persistence;
using WattWise.Infrastructure.Persistence.Schema;

namespace WattWise.Infrastructure.IntegrationTests;

/// <summary>Runs one real step on its own, with the context it was resolved with, to reach its connection and timeout branches.</summary>
public class SchemaStepTests(DatabaseFixture fixture) : IClassFixture<DatabaseFixture>
{
    private static CancellationToken Token => PlatformSql.Token;

    [Fact]
    public async Task The_hangfire_step_closes_the_connection_it_opened()
    {
        await using ServiceProvider provider = fixture.BuildServices(DatabaseRole.Cli);
        await using AsyncServiceScope scope = provider.CreateAsyncScope();
        AppDbContext db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        await StepOf(scope, "hangfire-storage").ApplyAsync(Token);

        Assert.Equal(ConnectionState.Closed, db.Database.GetDbConnection().State);
    }

    [Fact]
    public async Task The_hangfire_step_leaves_a_connection_it_did_not_open_open()
    {
        await using ServiceProvider provider = fixture.BuildServices(DatabaseRole.Cli);
        await using AsyncServiceScope scope = provider.CreateAsyncScope();
        AppDbContext db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await db.Database.OpenConnectionAsync(Token);

        await StepOf(scope, "hangfire-storage").ApplyAsync(Token);

        Assert.Equal(ConnectionState.Open, db.Database.GetDbConnection().State);
    }

    [Fact]
    public async Task The_hangfire_step_stops_on_a_cancelled_token()
    {
        await using ServiceProvider provider = fixture.BuildServices(DatabaseRole.Cli);
        await using AsyncServiceScope scope = provider.CreateAsyncScope();
        AppDbContext db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        // Opened beforehand, so the step's own check is what throws, not opening the connection.
        await db.Database.OpenConnectionAsync(Token);
        using CancellationTokenSource cancelled = new();
        await cancelled.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => StepOf(scope, "hangfire-storage").ApplyAsync(cancelled.Token));
    }

    [Fact]
    public async Task The_hangfire_step_closes_its_connection_when_the_install_fails()
    {
        // The api role has no rights on the hangfire schema, so the installer throws 42501 after the step opened the connection.
        await using ServiceProvider provider = fixture.BuildServices(DatabaseRole.Api);
        await using AsyncServiceScope scope = provider.CreateAsyncScope();
        AppDbContext db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        await Assert.ThrowsAsync<PostgresException>(() => StepOf(scope, "hangfire-storage").ApplyAsync(Token));

        Assert.Equal(ConnectionState.Closed, db.Database.GetDbConnection().State);
    }

    // characterization: EfCoreMigrationsStep.cs:15; the cli role has no statement_timeout either (postgres.md "Hardening").
    [Fact]
    public async Task The_ef_core_step_removes_the_command_timeout()
    {
        await using ServiceProvider provider = fixture.BuildServices(DatabaseRole.Cli);
        await using AsyncServiceScope scope = provider.CreateAsyncScope();
        AppDbContext db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        await StepOf(scope, "ef-core-migrations").ApplyAsync(Token);

        Assert.Equal((int?)0, db.Database.GetCommandTimeout());
    }

    private static ISchemaStep StepOf(AsyncServiceScope scope, string name) =>
        scope.ServiceProvider.GetServices<ISchemaStep>().Single(step => step.Name == name);
}
