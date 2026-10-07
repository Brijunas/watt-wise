using System.Diagnostics;

using Microsoft.Extensions.Logging;

using WattWise.Infrastructure.Observability;

namespace WattWise.Infrastructure.Persistence.Schema;

/// <summary>
/// The single migrate code path, used by <c>WattWise.Cli migrate</c> and the integration-test fixture.
/// It only runs the registered <see cref="ISchemaStep"/>s in order, each in its own span; what each
/// schema needs lives in its step.
/// </summary>
public sealed class DatabaseMigrator(IEnumerable<ISchemaStep> steps, ILogger<DatabaseMigrator> logger)
{
    public async Task MigrateAsync(CancellationToken cancellationToken)
    {
        foreach (ISchemaStep step in steps)
        {
            cancellationToken.ThrowIfCancellationRequested();
            logger.LogDebug("Running schema step {Step}.", step.Name);
            using Activity? activity = InfrastructureTelemetry.Source.StartActivity($"schema-step {step.Name}");
            activity?.SetTag("wattwise.schema_step", step.Name);
            try
            {
                await step.ApplyAsync(cancellationToken);
            }
            catch (Exception ex)
            {
                activity?.SetStatus(ActivityStatusCode.Error, $"Schema step {step.Name} failed.");
                activity?.AddException(ex);
                throw;
            }
        }
    }
}
