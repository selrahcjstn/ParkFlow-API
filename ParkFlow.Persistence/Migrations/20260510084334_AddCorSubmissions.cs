using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ParkFlow.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddCorSubmissions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "CorSubmissions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserAccountId = table.Column<Guid>(type: "uuid", nullable: false),
                    AcademicTerm = table.Column<string>(type: "text", nullable: false),
                    CorDocumentUrl = table.Column<string>(type: "text", nullable: false),
                    DateOfIncident = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    VerificationStatus = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CorSubmissions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CorSubmissions_UserAccounts_UserAccountId",
                        column: x => x.UserAccountId,
                        principalTable: "UserAccounts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CorSubmissions_UserAccountId",
                table: "CorSubmissions",
                column: "UserAccountId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CorSubmissions");
        }
    }
}
