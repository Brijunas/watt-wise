using Microsoft.Extensions.Logging;

namespace WattWise.Infrastructure.Persistence.Schema;

/// <summary>
/// The single migrate code path, used by <c>WattWise.Cli migrate</c> and the integration-test fixture.
/// It only runs the registered <see cref="ISchemaStep"/>s in order; what each schema needs lives in its step.
/// </summary>
public sealed class DatabaseMigrator(IEnumerable<ISchemaStep> steps, ILogger<DatabaseMigrator> logger)
{
    public async Task MigrateAsync(CancellationToken cancellationToken)
    {
        foreach (ISchemaStep step in steps)
        {
            cancellationToken.ThrowIfCancellationRequested();
            logger.LogDebug("Running schema step {Step}.", step.Name);
            await step.ApplyAsync(cancellationToken);
        }
    }
}
