using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ParkFlow.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddViolationSnapshotFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "LogId",
                table: "Violations");

            migrationBuilder.AddColumn<DateTime>(
                name: "RecordedEntryTime",
                table: "Violations",
                type: "timestamp with time zone",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<DateTime>(
                name: "RecordedExitTime",
                table: "Violations",
                type: "timestamp with time zone",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<int>(
                name: "RecordedOverstayMinutes",
                table: "Violations",
                type: "integer",
                nullable: false,
                defaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "RecordedEntryTime",
                table: "Violations");

            migrationBuilder.DropColumn(
                name: "RecordedExitTime",
                table: "Violations");

            migrationBuilder.DropColumn(
                name: "RecordedOverstayMinutes",
                table: "Violations");

            migrationBuilder.AddColumn<Guid>(
                name: "LogId",
                table: "Violations",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));
        }
    }
}
