using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ParkFlow.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class SeparateReservationVehicleType : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "ReservedVehicleType",
                table: "ParkingReservations",
                type: "integer",
                nullable: false,
                defaultValue: 0);
            migrationBuilder.Sql("""
                UPDATE "ParkingReservations" AS r
                SET "ReservedVehicleType" = 2
                FROM "Vehicles" AS v
                WHERE r."VehicleId" = v."Id" AND v."VehicleType" = 2;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ReservedVehicleType",
                table: "ParkingReservations");
        }
    }
}
