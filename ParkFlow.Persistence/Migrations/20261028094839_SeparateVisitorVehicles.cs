using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ParkFlow.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class SeparateVisitorVehicles : Migration
    {
        public const string PreflightSql = """
            LOCK TABLE "Visitors", "VisitSessions", "Vehicles" IN SHARE ROW EXCLUSIVE MODE;
            DO $visitor_backfill$
            BEGIN
              IF EXISTS (SELECT 1 FROM "Visitors" GROUP BY upper(replace(replace(btrim("PlateNumber"), ' ', ''), '-', '')) HAVING count(*) > 1)
                OR EXISTS (SELECT 1 FROM "Visitors" WHERE upper(replace(replace(btrim("PlateNumber"), ' ', ''), '-', '')) !~ '^[A-Z0-9]{2,20}$')
                OR EXISTS (SELECT 1 FROM "VisitSessions" WHERE "Status" = 0 GROUP BY "VisitorId" HAVING count(*) > 1)
                OR EXISTS (SELECT 1 FROM "VisitSessions" s LEFT JOIN "Visitors" v ON v."Id" = s."VisitorId" WHERE v."Id" IS NULL)
                OR EXISTS (SELECT 1 FROM "Visitors" v JOIN "Vehicles" r ON upper(replace(replace(btrim(v."PlateNumber"), ' ', ''), '-', '')) = upper(replace(replace(btrim(r."PlateNumber"), ' ', ''), '-', '')))
              THEN RAISE EXCEPTION 'Visitor vehicle backfill blocked: conflicting plates, invalid plates or ambiguous sessions. Manual reconciliation is required; no visitor records have been merged.';
              END IF;
            END $visitor_backfill$;
            """;

        public const string BackfillSql = """
            INSERT INTO "VisitorVehicles" ("Id", "VisitorId", "PlateNumber", "NormalizedPlateNumber", "VehicleType", "Brand", "VehiclePictureUrl", "CreatedAt", "UpdatedAt")
            SELECT "Id", "Id", "PlateNumber", upper(replace(replace(btrim("PlateNumber"), ' ', ''), '-', '')), "VehicleType", "Brand", "VehiclePictureUrl", "CreatedAt", "UpdatedAt" FROM "Visitors";
            UPDATE "VisitSessions" SET "VisitorVehicleId" = "VisitorId";
            """;

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(PreflightSql);
            migrationBuilder.AddColumn<Guid>(
                name: "VisitorVehicleId",
                table: "VisitSessions",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<DateTime>(
                name: "LicenseVerifiedAt",
                table: "Visitors",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "LicenseVerifiedBy",
                table: "Visitors",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "VisitorVehicles",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    VisitorId = table.Column<Guid>(type: "uuid", nullable: false),
                    PlateNumber = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    NormalizedPlateNumber = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    VehicleType = table.Column<int>(type: "integer", nullable: false),
                    Brand = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    VehiclePictureUrl = table.Column<string>(type: "text", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_VisitorVehicles", x => x.Id);
                    table.UniqueConstraint("AK_VisitorVehicles_Id_VisitorId", x => new { x.Id, x.VisitorId });
                    table.CheckConstraint("CK_VisitorVehicles_NormalizedPlate", "\"NormalizedPlateNumber\" = upper(replace(replace(btrim(\"PlateNumber\"), ' ', ''), '-', '')) AND \"NormalizedPlateNumber\" ~ '^[A-Z0-9]{2,20}$'");
                    table.ForeignKey(
                        name: "FK_VisitorVehicles_Visitors_VisitorId",
                        column: x => x.VisitorId,
                        principalTable: "Visitors",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.Sql(BackfillSql);

            migrationBuilder.CreateIndex(
                name: "IX_VisitSessions_VisitorVehicleId",
                table: "VisitSessions",
                column: "VisitorVehicleId",
                unique: true,
                filter: "\"Status\" = 0");

            migrationBuilder.CreateIndex(
                name: "IX_VisitSessions_VisitorVehicleId_VisitorId",
                table: "VisitSessions",
                columns: new[] { "VisitorVehicleId", "VisitorId" });

            migrationBuilder.CreateIndex(
                name: "IX_VisitorVehicles_NormalizedPlateNumber",
                table: "VisitorVehicles",
                column: "NormalizedPlateNumber",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_VisitorVehicles_VisitorId",
                table: "VisitorVehicles",
                column: "VisitorId");

            migrationBuilder.AddForeignKey(
                name: "FK_VisitSessions_VisitorVehicles_VisitorVehicleId_VisitorId",
                table: "VisitSessions",
                columns: new[] { "VisitorVehicleId", "VisitorId" },
                principalTable: "VisitorVehicles",
                principalColumns: new[] { "Id", "VisitorId" },
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            throw new NotSupportedException("Visitor vehicle rollback would discard ownership/session links. Preserve data and use a separately reviewed recovery plan.");
        }
    }
}
