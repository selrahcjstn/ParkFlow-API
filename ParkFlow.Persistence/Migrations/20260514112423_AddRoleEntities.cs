using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ParkFlow.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddRoleEntities : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Course",
                table: "UserProfiles");

            migrationBuilder.DropColumn(
                name: "Office",
                table: "UserProfiles");

            migrationBuilder.DropColumn(
                name: "Section",
                table: "UserProfiles");

            migrationBuilder.DropColumn(
                name: "YearLevel",
                table: "UserProfiles");

            migrationBuilder.DropColumn(
                name: "Roles",
                table: "UserAccounts");

            migrationBuilder.CreateTable(
                name: "Admins",
                columns: table => new
                {
                    UserProfileId = table.Column<Guid>(type: "uuid", nullable: false),
                    RoleLevel = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Admins", x => x.UserProfileId);
                    table.ForeignKey(
                        name: "FK_Admins_UserProfiles_UserProfileId",
                        column: x => x.UserProfileId,
                        principalTable: "UserProfiles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Guards",
                columns: table => new
                {
                    UserProfileId = table.Column<Guid>(type: "uuid", nullable: false),
                    AssignedGate = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Guards", x => x.UserProfileId);
                    table.ForeignKey(
                        name: "FK_Guards_UserProfiles_UserProfileId",
                        column: x => x.UserProfileId,
                        principalTable: "UserProfiles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Personnel",
                columns: table => new
                {
                    UserProfileId = table.Column<Guid>(type: "uuid", nullable: false),
                    IdCardNumber = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Department = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Personnel", x => x.UserProfileId);
                    table.ForeignKey(
                        name: "FK_Personnel_UserProfiles_UserProfileId",
                        column: x => x.UserProfileId,
                        principalTable: "UserProfiles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Students",
                columns: table => new
                {
                    UserProfileId = table.Column<Guid>(type: "uuid", nullable: false),
                    StudentNumber = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Course = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Section = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    YearLevel = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Students", x => x.UserProfileId);
                    table.ForeignKey(
                        name: "FK_Students_UserProfiles_UserProfileId",
                        column: x => x.UserProfileId,
                        principalTable: "UserProfiles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Admins_UserProfileId",
                table: "Admins",
                column: "UserProfileId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Guards_UserProfileId",
                table: "Guards",
                column: "UserProfileId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Personnel_IdCardNumber",
                table: "Personnel",
                column: "IdCardNumber",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Personnel_UserProfileId",
                table: "Personnel",
                column: "UserProfileId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Students_StudentNumber",
                table: "Students",
                column: "StudentNumber",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Students_UserProfileId",
                table: "Students",
                column: "UserProfileId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Admins");

            migrationBuilder.DropTable(
                name: "Guards");

            migrationBuilder.DropTable(
                name: "Personnel");

            migrationBuilder.DropTable(
                name: "Students");

            migrationBuilder.AddColumn<string>(
                name: "Course",
                table: "UserProfiles",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Office",
                table: "UserProfiles",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Section",
                table: "UserProfiles",
                type: "character varying(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "YearLevel",
                table: "UserProfiles",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "Roles",
                table: "UserAccounts",
                type: "integer",
                nullable: false,
                defaultValue: 0);
        }
    }
}
