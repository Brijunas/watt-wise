using System.Data;

using Hangfire.PostgreSql;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

using Npgsql;

namespace WattWise.Infrastructure.Persistence.Schema;

/// <summary>
/// Installs or upgrades Hangfire's tables in <see cref="DatabaseSchemas.Hangfire"/>. Hangfire's own
/// installer is idempotent and applies only the scripts the database is missing.
/// </summary>
internal sealed class HangfireStorageStep(AppDbContext db, ILogger<HangfireStorageStep> logger) : ISchemaStep
{
    public string Name => "hangfire-storage";

    public async Task ApplyAsync(CancellationToken cancellationToken)
    {
        // Borrow the migrating connection (cli acting as owner) so the objects belong to owner,
        // like the EF Core ones, instead of opening a second connection with its own settings.
        NpgsqlConnection connection = (NpgsqlConnection)db.Database.GetDbConnection();
        bool openedHere = connection.State != ConnectionState.Open;
        if (openedHere)
        {
            await connection.OpenAsync(cancellationToken);
        }

        try
        {
            // The installer is synchronous and takes no token, so cancellation is honoured up to here.
            cancellationToken.ThrowIfCancellationRequested();
            PostgreSqlObjectsInstaller.Install(connection, DatabaseSchemas.Hangfire);
            logger.LogInformation("Hangfire storage installed or up to date in schema {Schema}.", DatabaseSchemas.Hangfire);
        }
        finally
        {
            if (openedHere)
            {
                await connection.CloseAsync();
            }
        }
    }
}
