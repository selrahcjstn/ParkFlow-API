using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ParkFlow.Persistence.Migrations;

public partial class RetireFinancialContractsPreserveHistory : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        // Retire CLR mappings only. Historical tables, invoice columns and notifications
        // deliberately remain in PostgreSQL for older clients and a separately approved policy.
        migrationBuilder.Sql("""
            DO $history$
            BEGIN
                IF EXISTS (SELECT 1 FROM "__EFMigrationsHistory"
                    WHERE "MigrationId" = '20261027202102_RemovePaymentAndViolationHistory') THEN
                    RAISE EXCEPTION 'The deferred financial deletion migration was applied. Stop and resolve the migration-history conflict; do not rewrite applied migrations.';
                END IF;
                IF to_regclass('"Violations"') IS NULL OR
                    (SELECT count(*) FROM information_schema.columns
                     WHERE table_schema = current_schema() AND table_name = 'Feedbacks'
                     AND column_name IN ('InvoiceAmount', 'InvoiceDescription', 'InvoiceNumber', 'InvoiceStatus')) <> 4 THEN
                    RAISE EXCEPTION 'Expected historical financial storage is missing. Stop and inspect the target schema and backup before release.';
                END IF;
            END
            $history$;
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        // Physical storage was retained, so restoring the previous mappings needs no DDL.
    }
}
