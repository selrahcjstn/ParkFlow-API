using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ParkFlow.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class RecoveryAddVehicleTypeAndViolations : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "VehicleType",
                table: "Vehicles",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateTable(
                name: "Violations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    LogId = table.Column<Guid>(type: "uuid", nullable: false),
                    ParkingLogId = table.Column<Guid>(type: "uuid", nullable: false),
                    ViolationType = table.Column<int>(type: "integer", nullable: false),
                    PenaltyFee = table.Column<decimal>(type: "numeric", nullable: false),
                    SettlementStatus = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Violations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Violations_ParkingLogs_ParkingLogId",
                        column: x => x.ParkingLogId,
                        principalTable: "ParkingLogs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Violations_ParkingLogId",
                table: "Violations",
                column: "ParkingLogId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Violations");

            migrationBuilder.DropColumn(
                name: "VehicleType",
                table: "Vehicles");
        }
    }
}
