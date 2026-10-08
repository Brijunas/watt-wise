namespace WattWise.Infrastructure.Persistence.Schema;

/// <summary>
/// The PostgreSQL schemas the backend owns, one constant each. Every reference to a schema name goes
/// through here, so moving or splitting a schema starts in one place. Roles and grants per schema are in
/// deploy/postgres/bootstrap.sql.
/// </summary>
public static class DatabaseSchemas
{
    /// <summary>Application tables.</summary>
    public const string App = "app";

    /// <summary>EF Core's migrations history. Only the Cli (as owner) reaches it; no app role is granted anything on it.</summary>
    public const string Migrations = "migrations";

    /// <summary>Hangfire's job storage, used only by the Jobs host.</summary>
    public const string Hangfire = "hangfire";
}
