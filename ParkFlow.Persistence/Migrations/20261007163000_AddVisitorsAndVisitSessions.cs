using System;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ParkFlow.Persistence.Migrations
{
    /// <inheritdoc />
    [DbContext(typeof(AppDbContext))]
    [Migration("20261007163000_AddVisitorsAndVisitSessions")]
    public partial class AddVisitorsAndVisitSessions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Visitors",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    FullName = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    ContactNumber = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    PlateNumber = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    VehicleType = table.Column<int>(type: "integer", nullable: false),
                    Brand = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Visitors", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "VisitSessions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    VisitorId = table.Column<Guid>(type: "uuid", nullable: false),
                    EntryTime = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ExitTime = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    Purpose = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    Destination = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    EntryGuardId = table.Column<Guid>(type: "uuid", nullable: true),
                    ExitGuardId = table.Column<Guid>(type: "uuid", nullable: true),
                    EntryGate = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    ExitGate = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_VisitSessions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_VisitSessions_Guards_EntryGuardId",
                        column: x => x.EntryGuardId,
                        principalTable: "Guards",
                        principalColumn: "UserProfileId",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_VisitSessions_Guards_ExitGuardId",
                        column: x => x.ExitGuardId,
                        principalTable: "Guards",
                        principalColumn: "UserProfileId",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_VisitSessions_Visitors_VisitorId",
                        column: x => x.VisitorId,
                        principalTable: "Visitors",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Visitors_PlateNumber",
                table: "Visitors",
                column: "PlateNumber");

            migrationBuilder.CreateIndex(
                name: "IX_VisitSessions_EntryGuardId",
                table: "VisitSessions",
                column: "EntryGuardId");

            migrationBuilder.CreateIndex(
                name: "IX_VisitSessions_ExitGuardId",
                table: "VisitSessions",
                column: "ExitGuardId");

            migrationBuilder.CreateIndex(
                name: "IX_VisitSessions_VisitorId_Status",
                table: "VisitSessions",
                columns: new[] { "VisitorId", "Status" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "VisitSessions");

            migrationBuilder.DropTable(
                name: "Visitors");
        }
    }
}
