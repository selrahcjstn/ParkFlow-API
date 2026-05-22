using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ParkFlow.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddViolationStatusSnapshot : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "RecordedStatus",
                table: "Violations",
                type: "integer",
                nullable: false,
                defaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "RecordedStatus",
                table: "Violations");
        }
    }
}
