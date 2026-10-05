using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace WattWise.Infrastructure.Persistence;

/// <summary>
/// The single migrate code path, used by <c>WattWise.Cli migrate</c> and the integration-test fixture.
/// </summary>
public sealed class DatabaseMigrator(AppDbContext db, ILogger<DatabaseMigrator> logger)
{
    public async Task<IReadOnlyList<string>> MigrateAsync(CancellationToken cancellationToken)
    {
        // No command timeout: index builds and table rewrites can run long, and the cli role
        // has no statement_timeout either. Ctrl+C still cancels.
        db.Database.SetCommandTimeout(0);

        string[] pending = [.. await db.Database.GetPendingMigrationsAsync(cancellationToken)];
        if (pending.Length == 0)
        {
            logger.LogInformation("No pending migrations.");
            return [];
        }

        logger.LogInformation("Applying {Count} pending migration(s): {Migrations}", pending.Length, string.Join(", ", pending));
        await db.Database.MigrateAsync(cancellationToken);
        logger.LogInformation("Applied: {Migrations}", string.Join(", ", pending));
        return pending;
    }
}
