using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ParkFlow.Persistence.Migrations;

[DbContext(typeof(AppDbContext))]
[Migration("20261010090000_AddVisitorParkingCharges")]
public class AddVisitorParkingCharges : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AlterColumn<Guid>(name: "ParkingLogId", table: "Violations",
            type: "uuid", nullable: true, oldClrType: typeof(Guid), oldType: "uuid");
        migrationBuilder.AddColumn<Guid>(name: "VisitSessionId", table: "Violations",
            type: "uuid", nullable: true);
        migrationBuilder.CreateIndex(name: "IX_Violations_VisitSessionId", table: "Violations",
            column: "VisitSessionId", unique: true);
        migrationBuilder.AddForeignKey(name: "FK_Violations_VisitSessions_VisitSessionId", table: "Violations",
            column: "VisitSessionId", principalTable: "VisitSessions", principalColumn: "Id",
            onDelete: ReferentialAction.Cascade);
        migrationBuilder.AddCheckConstraint(name: "CK_Violations_Session", table: "Violations",
            sql: "(\"ParkingLogId\" IS NOT NULL) <> (\"VisitSessionId\" IS NOT NULL)");
        // Existing completed visits are not back-billed: their collection history is unknown.
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        // Do not silently discard collected visitor fees during a rollback.
        migrationBuilder.Sql("""
            DO $$ BEGIN
                IF EXISTS (SELECT 1 FROM "Violations" WHERE "VisitSessionId" IS NOT NULL) THEN
                    RAISE EXCEPTION 'Cannot roll back while visitor charges exist. Preserve and reconcile these financial records first.';
                END IF;
            END $$;
            """);
        migrationBuilder.DropCheckConstraint(name: "CK_Violations_Session", table: "Violations");
        migrationBuilder.DropForeignKey(name: "FK_Violations_VisitSessions_VisitSessionId", table: "Violations");
        migrationBuilder.DropIndex(name: "IX_Violations_VisitSessionId", table: "Violations");
        migrationBuilder.DropColumn(name: "VisitSessionId", table: "Violations");
        migrationBuilder.AlterColumn<Guid>(name: "ParkingLogId", table: "Violations",
            type: "uuid", nullable: false, oldClrType: typeof(Guid), oldType: "uuid", oldNullable: true);
    }
}
