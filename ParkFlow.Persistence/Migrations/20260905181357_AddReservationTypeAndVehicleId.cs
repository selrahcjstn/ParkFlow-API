using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ParkFlow.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddReservationTypeAndVehicleId : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "Type",
                table: "ParkingReservations",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<Guid>(
                name: "VehicleId",
                table: "ParkingReservations",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_ParkingReservations_VehicleId",
                table: "ParkingReservations",
                column: "VehicleId");

            migrationBuilder.AddForeignKey(
                name: "FK_ParkingReservations_Vehicles_VehicleId",
                table: "ParkingReservations",
                column: "VehicleId",
                principalTable: "Vehicles",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ParkingReservations_Vehicles_VehicleId",
                table: "ParkingReservations");

            migrationBuilder.DropIndex(
                name: "IX_ParkingReservations_VehicleId",
                table: "ParkingReservations");

            migrationBuilder.DropColumn(
                name: "Type",
                table: "ParkingReservations");

            migrationBuilder.DropColumn(
                name: "VehicleId",
                table: "ParkingReservations");
        }
    }
}
