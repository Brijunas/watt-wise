namespace WattWise.Infrastructure.Persistence.Schema;

/// <summary>
/// Brings one part of the database schema up to date. Each step owns one schema (or one tool's objects)
/// and must be idempotent: <see cref="DatabaseMigrator"/> runs every step on every migrate, whether or
/// not anything changed. To add a schema, add a step and register it in
/// <see cref="SchemaServiceCollectionExtensions.AddSchemaSteps"/>; nothing else changes.
/// </summary>
public interface ISchemaStep
{
    /// <summary>Short name used in logs and as the name of the step's span, e.g. <c>ef-core-migrations</c>.</summary>
    string Name { get; }

    Task ApplyAsync(CancellationToken cancellationToken);
}
