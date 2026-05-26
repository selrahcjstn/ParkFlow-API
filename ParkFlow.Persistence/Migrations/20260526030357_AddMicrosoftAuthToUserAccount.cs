using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ParkFlow.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddMicrosoftAuthToUserAccount : Migration
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

            migrationBuilder.AddColumn<int>(
                name: "AuthProvider",
                table: "UserAccounts",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "ExternalProviderId",
                table: "UserAccounts",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_UserAccounts_AuthProvider_ExternalProviderId",
                table: "UserAccounts",
                columns: new[] { "AuthProvider", "ExternalProviderId" },
                unique: true,
                filter: "\"ExternalProviderId\" IS NOT NULL");
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
