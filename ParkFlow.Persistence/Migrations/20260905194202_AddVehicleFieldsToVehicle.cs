using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ParkFlow.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddVehicleFieldsToVehicle : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "OrcrDocumentUrl",
                table: "Vehicles",
                type: "character varying(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "VehiclePictureUrl",
                table: "Vehicles",
                type: "character varying(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "VerificationStatus",
                table: "Vehicles",
                type: "integer",
                nullable: false,
                defaultValue: 1);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "OrcrDocumentUrl",
                table: "Vehicles");

            migrationBuilder.DropColumn(
                name: "VehiclePictureUrl",
                table: "Vehicles");

            migrationBuilder.DropColumn(
                name: "VerificationStatus",
                table: "Vehicles");
        }
    }
}
