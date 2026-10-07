using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace WattWise.Infrastructure.Persistence.Schema;

/// <summary>Applies pending EF Core migrations, which own the <see cref="DatabaseSchemas.App"/> schema.</summary>
internal sealed class EfCoreMigrationsStep(AppDbContext db, ILogger<EfCoreMigrationsStep> logger) : ISchemaStep
{
    public string Name => "ef-core-migrations";

    public async Task ApplyAsync(CancellationToken cancellationToken)
    {
        // No command timeout: index builds and table rewrites can run long, and the cli role
        // has no statement_timeout either. Ctrl+C still cancels.
        db.Database.SetCommandTimeout(0);

        string[] pending = [.. await db.Database.GetPendingMigrationsAsync(cancellationToken)];
        if (pending.Length == 0)
        {
            logger.LogInformation("No pending migrations.");
            return;
        }

        logger.LogInformation("Applying {Count} pending migration(s): {Migrations}", pending.Length, string.Join(", ", pending));
        await db.Database.MigrateAsync(cancellationToken);
        logger.LogInformation("Applied: {Migrations}", string.Join(", ", pending));
    }
}
