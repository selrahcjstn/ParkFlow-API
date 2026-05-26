using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ParkFlow.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class SyncMicrosoftAuthPending : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "PhoneNumber",
                table: "UserAccounts",
                type: "text",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "text");

            migrationBuilder.AlterColumn<string>(
                name: "PasswordHash",
                table: "UserAccounts",
                type: "text",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "text");

            migrationBuilder.Sql("ALTER TABLE \"UserAccounts\" ADD COLUMN IF NOT EXISTS \"AuthProvider\" integer NOT NULL DEFAULT 0;");
            migrationBuilder.Sql("ALTER TABLE \"UserAccounts\" ADD COLUMN IF NOT EXISTS \"ExternalProviderId\" character varying(200);");
            migrationBuilder.Sql("CREATE UNIQUE INDEX IF NOT EXISTS \"IX_UserAccounts_AuthProvider_ExternalProviderId\" ON \"UserAccounts\" (\"AuthProvider\", \"ExternalProviderId\") WHERE \"ExternalProviderId\" IS NOT NULL;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_UserAccounts_AuthProvider_ExternalProviderId",
                table: "UserAccounts");

            migrationBuilder.DropColumn(
                name: "AuthProvider",
                table: "UserAccounts");

            migrationBuilder.DropColumn(
                name: "ExternalProviderId",
                table: "UserAccounts");

            migrationBuilder.AlterColumn<string>(
                name: "PhoneNumber",
                table: "UserAccounts",
                type: "text",
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "text",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "PasswordHash",
                table: "UserAccounts",
                type: "text",
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "text",
                oldNullable: true);
        }
    }
}
