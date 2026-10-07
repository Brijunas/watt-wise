using Docker.DotNet.Models;

using DotNet.Testcontainers.Configurations;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

using Npgsql;

using Testcontainers.PostgreSql;

using WattWise.Infrastructure;
using WattWise.Infrastructure.Persistence.Schema;

namespace WattWise.Testing;

/// <summary>
/// Assembly fixture: one PostgreSQL container, bootstrapped and migrated like a deployment, that
/// serves as a template for the per-class databases.
/// </summary>
public sealed class PostgresContainerFixture : IAsyncLifetime
{
    private const string AdminUser = "admin";
    private const string TemplateDatabase = "wattwise";
    private const string ContainerScriptDirectory = "/tmp";
    private const string ContainerScriptPath = ContainerScriptDirectory + "/bootstrap.sql";

    private readonly string adminPassword = Guid.NewGuid().ToString("N");
    private readonly Dictionary<DatabaseRole, string> passwords = new()
    {
        [DatabaseRole.Cli] = Guid.NewGuid().ToString("N"),
        [DatabaseRole.Api] = Guid.NewGuid().ToString("N"),
        [DatabaseRole.Hangfire] = Guid.NewGuid().ToString("N"),
    };
    private readonly SemaphoreSlim createLock = new(1, 1);
    private const int StartAttempts = 5;

    private PostgreSqlContainer container = null!;

    // Rootless Docker with the pasta network driver forwards no host port in the ephemeral range
    // (32768 and up), which is where Docker and Testcontainers publish random ports, Ryuk's
    // included. So the container gets a fixed port below that range and Ryuk is switched off; the
    // wattwise.tests label finds the containers a killed run leaves behind.
    static PostgresContainerFixture() => TestcontainersSettings.ResourceReaperEnabled = false;

    public async ValueTask InitializeAsync()
    {
        // IAsyncLifetime passes no token; the test run's token cancels setup when the run is aborted.
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        await StartContainerAsync(cancellationToken);
        await RunBootstrapAsync(cancellationToken);
        await SetPasswordsAsync(cancellationToken);
        await MigrateAsync(cancellationToken);
    }

    // Parallel test assemblies can take the chosen port first, so a failed start is retried on another.
    private async Task StartContainerAsync(CancellationToken cancellationToken)
    {
        for (int attempt = 1; ; attempt++)
        {
            container = BuildContainer(FreeHostPort.Pick());
            try
            {
                await container.StartAsync(cancellationToken);
                return;
            }
            catch (Exception) when (attempt < StartAttempts)
            {
                await container.DisposeAsync();
            }
        }
    }

    private PostgreSqlContainer BuildContainer(int hostPort) =>
        new PostgreSqlBuilder("postgres:18.6-trixie")
            .WithUsername(AdminUser)
            .WithPassword(adminPassword)
            .WithDatabase("postgres")
            .WithCommand("-c", "shared_preload_libraries=pg_stat_statements")
            .WithLabel("wattwise.tests", "true")
            .WithCreateParameterModifier(parameters =>
            {
                PortBinding binding = new() { HostIP = "127.0.0.1", HostPort = hostPort.ToString(System.Globalization.CultureInfo.InvariantCulture) };
                parameters.HostConfig ??= new HostConfig();
                parameters.HostConfig.PortBindings ??= new Dictionary<string, IList<PortBinding>>();
                parameters.HostConfig.PortBindings[$"{PostgreSqlBuilder.PostgreSqlPort}/tcp"] = [binding];
            })
            .Build();

    // Cleanup takes no token: it must finish even when the run was cancelled.
    public async ValueTask DisposeAsync()
    {
        createLock.Dispose();
        await container.DisposeAsync();
    }

    /// <summary>Clones the migrated template into a new database owned by <c>owner</c>.</summary>
    public async Task<TestDatabase> CreateDatabaseAsync(CancellationToken cancellationToken)
    {
        string name = $"wattwise_{Guid.NewGuid():N}";
        await createLock.WaitAsync(cancellationToken);
        try
        {
            await using NpgsqlConnection connection = await OpenAdminConnectionAsync(cancellationToken);
            await ExecuteAsync(connection, $"CREATE DATABASE \"{name}\" TEMPLATE {TemplateDatabase} OWNER \"owner\"", cancellationToken);
            // Database-level ACLs are not copied by TEMPLATE, so the bootstrap's are applied again.
            await ExecuteAsync(connection, $"REVOKE ALL ON DATABASE \"{name}\" FROM PUBLIC", cancellationToken);
            await ExecuteAsync(connection, $"GRANT CONNECT ON DATABASE \"{name}\" TO cli, api, hangfire, backup", cancellationToken);
        }
        finally
        {
            createLock.Release();
        }

        return new TestDatabase(name, container.Hostname, container.GetMappedPublicPort(5432), AdminConnectionString, passwords);
    }

    private string AdminConnectionString => new NpgsqlConnectionStringBuilder
    {
        Host = container.Hostname,
        Port = container.GetMappedPublicPort(5432),
        Database = "postgres",
        Username = AdminUser,
        Password = adminPassword,
        Pooling = false,
    }.ConnectionString;

    private async Task RunBootstrapAsync(CancellationToken cancellationToken)
    {
        string script = Path.Combine(AppContext.BaseDirectory, "bootstrap.sql");
        await container.CopyAsync(script, ContainerScriptDirectory, ct: cancellationToken);

        DotNet.Testcontainers.Containers.ExecResult result = await container.ExecAsync(
            ["psql", "-v", "ON_ERROR_STOP=1", "-U", AdminUser, "-d", "postgres", "-f", ContainerScriptPath],
            cancellationToken);
        if (result.ExitCode != 0)
        {
            throw new InvalidOperationException($"bootstrap.sql failed with exit code {result.ExitCode}: {result.Stderr}");
        }
    }

    private async Task SetPasswordsAsync(CancellationToken cancellationToken)
    {
        await using NpgsqlConnection connection = await OpenAdminConnectionAsync(cancellationToken);
        foreach ((DatabaseRole role, string password) in passwords)
        {
            await ExecuteAsync(connection, $"ALTER ROLE {role.ToString().ToLowerInvariant()} PASSWORD '{password}'", cancellationToken);
        }
    }

    private async Task MigrateAsync(CancellationToken cancellationToken)
    {
        TestDatabase template = new(TemplateDatabase, container.Hostname, container.GetMappedPublicPort(5432), AdminConnectionString, passwords);
        IConfiguration configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(template.ConfigurationFor(DatabaseRole.Cli))
            .Build();
        ServiceCollection services = new();
        services.AddSingleton(configuration);
        services.AddLogging();
        services.AddInfrastructure();

        await using (ServiceProvider provider = services.BuildServiceProvider())
        {
            await using AsyncServiceScope scope = provider.CreateAsyncScope();
            await scope.ServiceProvider.GetRequiredService<DatabaseMigrator>().MigrateAsync(cancellationToken);
        }

        await DisconnectSessionsAsync(TemplateDatabase, cancellationToken);
    }

    // A template must have no sessions. EF Core keeps its own Npgsql data source alive past the
    // service provider, and ClearAllPools doesn't reach it, so the idle connection is ended server-side.
    private async Task DisconnectSessionsAsync(string database, CancellationToken cancellationToken)
    {
        await using NpgsqlConnection connection = await OpenAdminConnectionAsync(cancellationToken);
        await using NpgsqlCommand command = new(
            "SELECT count(pg_terminate_backend(pid)) FROM pg_stat_activity WHERE datname = @name AND pid <> pg_backend_pid()",
            connection);
        command.Parameters.AddWithValue("name", database);
        using CancellationTokenSource timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(10));
        // Termination is signalled, not waited for, so repeat until no session is left.
        while ((long)(await command.ExecuteScalarAsync(timeout.Token))! > 0)
        {
            await Task.Delay(50, timeout.Token);
        }
    }

    private async Task<NpgsqlConnection> OpenAdminConnectionAsync(CancellationToken cancellationToken)
    {
        NpgsqlConnection connection = new(AdminConnectionString);
        await connection.OpenAsync(cancellationToken);
        return connection;
    }

    private static async Task ExecuteAsync(NpgsqlConnection connection, string sql, CancellationToken cancellationToken)
    {
        await using NpgsqlCommand command = new(sql, connection);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }
}
