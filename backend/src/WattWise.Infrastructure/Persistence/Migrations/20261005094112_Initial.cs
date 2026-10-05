using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace WattWise.Infrastructure.Persistence.Migrations;

/// <inheritdoc />
public partial class _20261005094112_Initial : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        // Default privileges give api DML on every table owner creates, including the
        // history table. Only the Cli (as owner) may touch migration history.
        migrationBuilder.Sql("REVOKE ALL ON app.__ef_migrations_history FROM api;");
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("GRANT SELECT, INSERT, UPDATE, DELETE ON app.__ef_migrations_history TO api;");
    }
}
