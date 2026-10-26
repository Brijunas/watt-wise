using System;
using Microsoft.EntityFrameworkCore.Migrations;
using NodaTime;

#nullable disable

namespace WattWise.Infrastructure.Persistence.Migrations;

/// <inheritdoc />
public partial class _20261026161112_Initial : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.EnsureSchema(
            name: "app");

        migrationBuilder.CreateTable(
            name: "accounts",
            schema: "app",
            columns: table => new
            {
                id = table.Column<Guid>(type: "uuid", nullable: false),
                preferred_language = table.Column<string>(type: "text", nullable: false),
                created_at = table.Column<Instant>(type: "timestamp with time zone", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("pk_accounts", x => x.id);
            });

        migrationBuilder.CreateTable(
            name: "permissions",
            schema: "app",
            columns: table => new
            {
                id = table.Column<Guid>(type: "uuid", nullable: false),
                name = table.Column<string>(type: "text", nullable: false),
                description = table.Column<string>(type: "text", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("pk_permissions", x => x.id);
            });

        migrationBuilder.CreateTable(
            name: "roles",
            schema: "app",
            columns: table => new
            {
                id = table.Column<Guid>(type: "uuid", nullable: false),
                name = table.Column<string>(type: "text", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("pk_roles", x => x.id);
            });

        migrationBuilder.CreateTable(
            name: "identity_users",
            schema: "app",
            columns: table => new
            {
                id = table.Column<Guid>(type: "uuid", nullable: false),
                account_id = table.Column<Guid>(type: "uuid", nullable: false),
                email = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                user_name = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                normalized_user_name = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                normalized_email = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                email_confirmed = table.Column<bool>(type: "boolean", nullable: false),
                password_hash = table.Column<string>(type: "text", nullable: true),
                security_stamp = table.Column<string>(type: "text", nullable: true),
                concurrency_stamp = table.Column<string>(type: "text", nullable: true),
                phone_number = table.Column<string>(type: "text", nullable: true),
                phone_number_confirmed = table.Column<bool>(type: "boolean", nullable: false),
                two_factor_enabled = table.Column<bool>(type: "boolean", nullable: false),
                lockout_end = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                lockout_enabled = table.Column<bool>(type: "boolean", nullable: false),
                access_failed_count = table.Column<int>(type: "integer", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("pk_identity_users", x => x.id);
                table.ForeignKey(
                    name: "fk_identity_users_accounts_account_id",
                    column: x => x.account_id,
                    principalSchema: "app",
                    principalTable: "accounts",
                    principalColumn: "id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateTable(
            name: "account_roles",
            schema: "app",
            columns: table => new
            {
                account_id = table.Column<Guid>(type: "uuid", nullable: false),
                role_id = table.Column<Guid>(type: "uuid", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("pk_account_roles", x => new { x.account_id, x.role_id });
                table.ForeignKey(
                    name: "fk_account_roles_accounts_account_id",
                    column: x => x.account_id,
                    principalSchema: "app",
                    principalTable: "accounts",
                    principalColumn: "id",
                    onDelete: ReferentialAction.Cascade);
                table.ForeignKey(
                    name: "fk_account_roles_roles_role_id",
                    column: x => x.role_id,
                    principalSchema: "app",
                    principalTable: "roles",
                    principalColumn: "id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateTable(
            name: "role_permissions",
            schema: "app",
            columns: table => new
            {
                role_id = table.Column<Guid>(type: "uuid", nullable: false),
                permission_id = table.Column<Guid>(type: "uuid", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("pk_role_permissions", x => new { x.role_id, x.permission_id });
                table.ForeignKey(
                    name: "fk_role_permissions_permissions_permission_id",
                    column: x => x.permission_id,
                    principalSchema: "app",
                    principalTable: "permissions",
                    principalColumn: "id",
                    onDelete: ReferentialAction.Cascade);
                table.ForeignKey(
                    name: "fk_role_permissions_roles_role_id",
                    column: x => x.role_id,
                    principalSchema: "app",
                    principalTable: "roles",
                    principalColumn: "id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateTable(
            name: "email_link_tokens",
            schema: "app",
            columns: table => new
            {
                identity_id = table.Column<Guid>(type: "uuid", nullable: false),
                purpose = table.Column<string>(type: "text", nullable: false),
                token_hash = table.Column<byte[]>(type: "bytea", nullable: false),
                expires_at = table.Column<Instant>(type: "timestamp with time zone", nullable: false),
                created_at = table.Column<Instant>(type: "timestamp with time zone", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("pk_email_link_tokens", x => new { x.identity_id, x.purpose });
                table.ForeignKey(
                    name: "fk_email_link_tokens_identity_users_identity_id",
                    column: x => x.identity_id,
                    principalSchema: "app",
                    principalTable: "identity_users",
                    principalColumn: "id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateTable(
            name: "identity_user_logins",
            schema: "app",
            columns: table => new
            {
                login_provider = table.Column<string>(type: "text", nullable: false),
                provider_key = table.Column<string>(type: "text", nullable: false),
                provider_display_name = table.Column<string>(type: "text", nullable: true),
                user_id = table.Column<Guid>(type: "uuid", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("pk_identity_user_logins", x => new { x.login_provider, x.provider_key });
                table.ForeignKey(
                    name: "fk_identity_user_logins_identity_users_user_id",
                    column: x => x.user_id,
                    principalSchema: "app",
                    principalTable: "identity_users",
                    principalColumn: "id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateTable(
            name: "identity_user_tokens",
            schema: "app",
            columns: table => new
            {
                user_id = table.Column<Guid>(type: "uuid", nullable: false),
                login_provider = table.Column<string>(type: "text", nullable: false),
                name = table.Column<string>(type: "text", nullable: false),
                value = table.Column<string>(type: "text", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("pk_identity_user_tokens", x => new { x.user_id, x.login_provider, x.name });
                table.ForeignKey(
                    name: "fk_identity_user_tokens_identity_users_user_id",
                    column: x => x.user_id,
                    principalSchema: "app",
                    principalTable: "identity_users",
                    principalColumn: "id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateIndex(
            name: "ix_account_roles_role_id",
            schema: "app",
            table: "account_roles",
            column: "role_id");

        migrationBuilder.CreateIndex(
            name: "ix_identity_user_logins_user_id",
            schema: "app",
            table: "identity_user_logins",
            column: "user_id");

        migrationBuilder.CreateIndex(
            name: "ix_identity_users_account_id",
            schema: "app",
            table: "identity_users",
            column: "account_id",
            unique: true);

        migrationBuilder.CreateIndex(
            name: "ix_identity_users_normalized_email",
            schema: "app",
            table: "identity_users",
            column: "normalized_email",
            unique: true);

        migrationBuilder.CreateIndex(
            name: "ix_identity_users_normalized_user_name",
            schema: "app",
            table: "identity_users",
            column: "normalized_user_name",
            unique: true);

        migrationBuilder.CreateIndex(
            name: "ix_permissions_name",
            schema: "app",
            table: "permissions",
            column: "name",
            unique: true);

        migrationBuilder.CreateIndex(
            name: "ix_role_permissions_permission_id",
            schema: "app",
            table: "role_permissions",
            column: "permission_id");

        migrationBuilder.CreateIndex(
            name: "ix_roles_name",
            schema: "app",
            table: "roles",
            column: "name",
            unique: true);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "account_roles",
            schema: "app");

        migrationBuilder.DropTable(
            name: "email_link_tokens",
            schema: "app");

        migrationBuilder.DropTable(
            name: "identity_user_logins",
            schema: "app");

        migrationBuilder.DropTable(
            name: "identity_user_tokens",
            schema: "app");

        migrationBuilder.DropTable(
            name: "role_permissions",
            schema: "app");

        migrationBuilder.DropTable(
            name: "identity_users",
            schema: "app");

        migrationBuilder.DropTable(
            name: "permissions",
            schema: "app");

        migrationBuilder.DropTable(
            name: "roles",
            schema: "app");

        migrationBuilder.DropTable(
            name: "accounts",
            schema: "app");
    }
}
