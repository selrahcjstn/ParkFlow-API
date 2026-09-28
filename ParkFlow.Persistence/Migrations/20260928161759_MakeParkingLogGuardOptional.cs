using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ParkFlow.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class MakeParkingLogGuardOptional : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ParkingLogs_Guards_GuardId",
                table: "ParkingLogs");

            migrationBuilder.AlterColumn<Guid>(
                name: "GuardId",
                table: "ParkingLogs",
                type: "uuid",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uuid");

            migrationBuilder.AddForeignKey(
                name: "FK_ParkingLogs_Guards_GuardId",
                table: "ParkingLogs",
                column: "GuardId",
                principalTable: "Guards",
                principalColumn: "UserProfileId",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ParkingLogs_Guards_GuardId",
                table: "ParkingLogs");

            migrationBuilder.AlterColumn<Guid>(
                name: "GuardId",
                table: "ParkingLogs",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldNullable: true);

            migrationBuilder.AddForeignKey(
                name: "FK_ParkingLogs_Guards_GuardId",
                table: "ParkingLogs",
                column: "GuardId",
                principalTable: "Guards",
                principalColumn: "UserProfileId",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
