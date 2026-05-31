using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ParkFlow.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class MovePrimaryEmailToAuthIdentity : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_AuthIdentities_UserAccountId",
                table: "AuthIdentities");

            migrationBuilder.AddColumn<bool>(
                name: "IsPrimary",
                table: "AuthIdentities",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.Sql("""
                UPDATE "AuthIdentities" AS ai
                SET "IsPrimary" = true
                FROM "UserAccounts" AS ua
                WHERE ai."UserAccountId" = ua."Id"
                  AND ai."Email" IS NOT NULL
                  AND lower(ai."Email") = lower(ua."Email");
                """);

            migrationBuilder.Sql("""
                WITH ranked_identities AS (
                    SELECT
                        ai."Id",
                        row_number() OVER (
                            PARTITION BY ai."UserAccountId"
                            ORDER BY ai."Provider", ai."CreatedAt", ai."Id"
                        ) AS rn
                    FROM "AuthIdentities" AS ai
                    WHERE NOT EXISTS (
                        SELECT 1
                        FROM "AuthIdentities" AS primary_ai
                        WHERE primary_ai."UserAccountId" = ai."UserAccountId"
                          AND primary_ai."IsPrimary" = true
                    )
                )
                UPDATE "AuthIdentities" AS ai
                SET "IsPrimary" = true
                FROM ranked_identities AS ranked
                WHERE ai."Id" = ranked."Id"
                  AND ranked.rn = 1;
                """);

            migrationBuilder.DropColumn(
                name: "Email",
                table: "UserAccounts");

            migrationBuilder.CreateIndex(
                name: "IX_AuthIdentities_UserAccountId",
                table: "AuthIdentities",
                column: "UserAccountId",
                unique: true,
                filter: "\"IsPrimary\" = true");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_AuthIdentities_UserAccountId",
                table: "AuthIdentities");

            migrationBuilder.DropColumn(
                name: "IsPrimary",
                table: "AuthIdentities");

            migrationBuilder.AddColumn<string>(
                name: "Email",
                table: "UserAccounts",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.Sql("""
                UPDATE "UserAccounts" AS ua
                SET "Email" = COALESCE(ai."Email", '')
                FROM "AuthIdentities" AS ai
                WHERE ai."UserAccountId" = ua."Id"
                  AND ai."IsPrimary" = true;
                """);

            migrationBuilder.CreateIndex(
                name: "IX_AuthIdentities_UserAccountId",
                table: "AuthIdentities",
                column: "UserAccountId");
        }
    }
}
