using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace ParkFlow.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddDriverDocumentsAndDurableSettings : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "DriversLicenseUrl",
                table: "VisitSessions",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "LicenseVerifiedAt",
                table: "VisitSessions",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "LicenseVerifiedBy",
                table: "VisitSessions",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ProfilePictureUrl",
                table: "VisitSessions",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "VehiclePictureUrl",
                table: "VisitSessions",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DriversLicenseUrl",
                table: "Visitors",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ProfilePictureUrl",
                table: "Visitors",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "VehiclePictureUrl",
                table: "Visitors",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DriversLicenseUrl",
                table: "UserProfiles",
                type: "character varying(2048)",
                maxLength: 2048,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "LicenseRejectionReason",
                table: "UserProfiles",
                type: "character varying(2000)",
                maxLength: 2000,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "LicenseReviewedAt",
                table: "UserProfiles",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "LicenseReviewedBy",
                table: "UserProfiles",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "LicenseStatus",
                table: "UserProfiles",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateTable(
                name: "SystemSettingsDocuments",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Json = table.Column<string>(type: "text", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SystemSettingsDocuments", x => x.Id);
                });

            // Preserve the configured capacities, tariffs and academic term captured from the local API.
            migrationBuilder.InsertData("SystemSettingsDocuments", new[] { "Id", "Json", "UpdatedAt" },
                new object[] { 1, @"{""violationRatePerHour"":100.0,""feeCalculationMode"":""per_hour"",""baseFee"":50.0,""personnelFreeParkingStart"":""05:00"",""personnelFreeParkingEnd"":""21:00"",""isGracePeriodEnabled"":true,""gracePeriodMinutes"":15,""isEarlyParkingAllowed"":true,""earlyParkingMinutes"":15,""academicYear"":""2026-2027"",""currentSemester"":""1st Semester"",""lastResetDate"":""2026-10-21T09:52:03.8088339Z"",""carParking"":{""totalCapacity"":250,""reservationAllocationSlots"":75,""violationRatePerHour"":100,""baseFee"":50,""entryFee"":20,""dailyOverstayFee"":100},""motorcycleParking"":{""totalCapacity"":250,""reservationAllocationSlots"":75,""violationRatePerHour"":100,""baseFee"":50,""entryFee"":20,""dailyOverstayFee"":100},""maxParkingHours"":8,""totalCapacity"":500,""reservationAllocationSlots"":150,""maxVehiclesPerUser"":5,""maintenanceMode"":false,""rfidInstantScanEnabled"":true,""autoApproveVerification"":false}", new DateTime(2026, 10, 21, 0, 0, 0, DateTimeKind.Utc) });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "SystemSettingsDocuments");

            migrationBuilder.DropColumn(
                name: "DriversLicenseUrl",
                table: "VisitSessions");

            migrationBuilder.DropColumn(
                name: "LicenseVerifiedAt",
                table: "VisitSessions");

            migrationBuilder.DropColumn(
                name: "LicenseVerifiedBy",
                table: "VisitSessions");

            migrationBuilder.DropColumn(
                name: "ProfilePictureUrl",
                table: "VisitSessions");

            migrationBuilder.DropColumn(
                name: "VehiclePictureUrl",
                table: "VisitSessions");

            migrationBuilder.DropColumn(
                name: "DriversLicenseUrl",
                table: "Visitors");

            migrationBuilder.DropColumn(
                name: "ProfilePictureUrl",
                table: "Visitors");

            migrationBuilder.DropColumn(
                name: "VehiclePictureUrl",
                table: "Visitors");

            migrationBuilder.DropColumn(
                name: "DriversLicenseUrl",
                table: "UserProfiles");

            migrationBuilder.DropColumn(
                name: "LicenseRejectionReason",
                table: "UserProfiles");

            migrationBuilder.DropColumn(
                name: "LicenseReviewedAt",
                table: "UserProfiles");

            migrationBuilder.DropColumn(
                name: "LicenseReviewedBy",
                table: "UserProfiles");

            migrationBuilder.DropColumn(
                name: "LicenseStatus",
                table: "UserProfiles");
        }
    }
}
