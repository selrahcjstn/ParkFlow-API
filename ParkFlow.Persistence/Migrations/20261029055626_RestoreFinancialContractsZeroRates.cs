using Microsoft.EntityFrameworkCore.Migrations;
#nullable disable
namespace ParkFlow.Persistence.Migrations;
public partial class RestoreFinancialContractsZeroRates : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder) => migrationBuilder.Sql("""
        DO $restore$
        DECLARE saved record; document jsonb; category text; rate text; previous text;
        BEGIN
          IF to_regclass('"Violations"') IS NULL OR
            (SELECT count(*) FROM information_schema.columns WHERE table_schema=current_schema() AND table_name='Feedbacks'
             AND column_name IN ('InvoiceAmount','InvoiceDescription','InvoiceNumber','InvoiceStatus')) <> 4 THEN
            RAISE EXCEPTION 'Historical financial storage must exist. Restore from the approved backup; do not recreate or discard history.';
          END IF;
          FOR saved IN SELECT "Id","Json" FROM "SystemSettingsDocuments" FOR UPDATE LOOP
            document := saved."Json"::jsonb;
            previous := document::text;
            IF length(previous)>3900 THEN RAISE EXCEPTION 'Settings snapshot exceeds the audit limit; arrange an approved settings backup before migration.'; END IF;
            INSERT INTO "VerificationAudits" ("Id","Action","Details","CreatedAt")
              VALUES (gen_random_uuid(),'FeeRatesInitializedToZero',previous,now());
            FOREACH rate IN ARRAY ARRAY['ViolationRatePerHour','BaseFee','violationRatePerHour','baseFee'] LOOP
              IF document ? rate OR rate IN ('ViolationRatePerHour','BaseFee') THEN
                document := jsonb_set(document,ARRAY[rate],'0'::jsonb,true);
              END IF;
            END LOOP;
            FOREACH category IN ARRAY ARRAY['CarParking','MotorcycleParking','carParking','motorcycleParking'] LOOP
              IF document ? category THEN
                FOREACH rate IN ARRAY ARRAY['EntryFee','BaseFee','DailyOverstayFee','ViolationRatePerHour','StudentEntryFee','FacultyEntryFee','PersonnelEntryFee','VisitorFee','VipEntryFee',
                  'entryFee','baseFee','dailyOverstayFee','violationRatePerHour','studentEntryFee','facultyEntryFee','personnelEntryFee','visitorFee','vipEntryFee'] LOOP
                  IF (document->category) ? rate OR left(rate,1)=upper(left(rate,1)) THEN
                    document := jsonb_set(document,ARRAY[category,rate],'0'::jsonb,true);
                  END IF;
                END LOOP;
              END IF;
            END LOOP;
            UPDATE "SystemSettingsDocuments" SET "Json"=document::text,"UpdatedAt"=now() WHERE "Id"=saved."Id";
          END LOOP;
        END $restore$;
        """);
    protected override void Down(MigrationBuilder migrationBuilder) =>
        throw new System.NotSupportedException("Financial history and configured rates must be preserved. Use a reviewed forward recovery migration.");
}
