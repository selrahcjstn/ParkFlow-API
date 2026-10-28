using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ParkFlow.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class EnsureCorDocumentColumns : Migration
    {
        public const string CompatibilitySql = """
            DO $columns$
            DECLARE field text;
            BEGIN
              FOREACH field IN ARRAY ARRAY['OrcrDocumentUrl','MotorPictureUrl'] LOOP
                IF EXISTS (SELECT 1 FROM information_schema.columns WHERE table_schema=current_schema() AND table_name='CorSubmissions' AND column_name=field
                    AND (is_nullable <> 'YES' OR data_type NOT IN ('text','character varying') OR character_maximum_length < 2048)) THEN
                  RAISE EXCEPTION 'COR document column compatibility conflict. Inspect schema and existing data; no columns or values were overwritten.';
                END IF;
              END LOOP;
            END $columns$;
            ALTER TABLE "CorSubmissions" ADD COLUMN IF NOT EXISTS "OrcrDocumentUrl" character varying(2048) NULL;
            ALTER TABLE "CorSubmissions" ADD COLUMN IF NOT EXISTS "MotorPictureUrl" character varying(2048) NULL;
            """;

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(CompatibilitySql);

        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            throw new NotSupportedException("COR document columns and their values must be preserved. Use a separately reviewed recovery plan.");

        }
    }
}
