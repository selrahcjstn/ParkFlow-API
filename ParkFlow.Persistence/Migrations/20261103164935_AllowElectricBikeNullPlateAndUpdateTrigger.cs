using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ParkFlow.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AllowElectricBikeNullPlateAndUpdateTrigger : Migration
    {
        public const string UpdatePlateOwnershipTriggersSql = """
            CREATE OR REPLACE FUNCTION "MaintainVehiclePlateOwnership"() RETURNS trigger LANGUAGE plpgsql AS $maintain$
            DECLARE canonical text;
            BEGIN
              IF NEW."PlateNumber" IS NULL OR length(btrim(NEW."PlateNumber")) = 0 THEN
                IF TG_OP = 'UPDATE' THEN
                  IF TG_TABLE_NAME = 'Vehicles' THEN
                    DELETE FROM "VehiclePlateOwnership" WHERE "VehicleId" = NEW."Id";
                  ELSE
                    DELETE FROM "VehiclePlateOwnership" WHERE "VisitorVehicleId" = NEW."Id";
                  END IF;
                END IF;
                RETURN NEW;
              END IF;
              canonical := "CanonicalVehiclePlate"(NEW."PlateNumber");
              IF TG_OP = 'INSERT' THEN
                IF TG_TABLE_NAME = 'Vehicles' THEN
                  INSERT INTO "VehiclePlateOwnership" VALUES (canonical, NEW."Id", NULL);
                ELSE
                  INSERT INTO "VehiclePlateOwnership" VALUES (canonical, NULL, NEW."Id");
                END IF;
              ELSE
                IF TG_TABLE_NAME = 'Vehicles' THEN
                  INSERT INTO "VehiclePlateOwnership" ("NormalizedPlateNumber", "VehicleId", "VisitorVehicleId")
                  VALUES (canonical, NEW."Id", NULL)
                  ON CONFLICT ("VehicleId") DO UPDATE SET "NormalizedPlateNumber" = EXCLUDED."NormalizedPlateNumber";
                ELSE
                  INSERT INTO "VehiclePlateOwnership" ("NormalizedPlateNumber", "VehicleId", "VisitorVehicleId")
                  VALUES (canonical, NULL, NEW."Id")
                  ON CONFLICT ("VisitorVehicleId") DO UPDATE SET "NormalizedPlateNumber" = EXCLUDED."NormalizedPlateNumber";
                END IF;
              END IF;
              RETURN NEW;
            END $maintain$;

            CREATE OR REPLACE FUNCTION "ValidateVehiclePlateOwnership"() RETURNS trigger LANGUAGE plpgsql AS $validate$
            DECLARE canonical text; owner_id uuid; registered boolean; iteration integer;
            BEGIN
              FOR iteration IN 1..CASE WHEN TG_OP = 'UPDATE' THEN 2 ELSE 1 END LOOP
                IF TG_OP = 'DELETE' OR (TG_OP = 'UPDATE' AND iteration = 2) THEN
                  registered := OLD."VehicleId" IS NOT NULL;
                  owner_id := coalesce(OLD."VehicleId", OLD."VisitorVehicleId");
                ELSE
                  registered := NEW."VehicleId" IS NOT NULL;
                  owner_id := coalesce(NEW."VehicleId", NEW."VisitorVehicleId");
                END IF;
                IF registered THEN
                  SELECT "CanonicalVehiclePlate"("PlateNumber") INTO canonical FROM "Vehicles" WHERE "Id" = owner_id;
                  IF FOUND THEN
                    IF canonical IS NULL THEN
                      IF EXISTS (SELECT 1 FROM "VehiclePlateOwnership" WHERE "VehicleId" = owner_id) THEN
                        RAISE EXCEPTION 'Plate ownership must match its source vehicle.' USING ERRCODE = '23514';
                      END IF;
                    ELSIF NOT EXISTS (SELECT 1 FROM "VehiclePlateOwnership" WHERE "VehicleId" = owner_id AND "NormalizedPlateNumber" = canonical) THEN
                      RAISE EXCEPTION 'Plate ownership must match its source vehicle.' USING ERRCODE = '23514';
                    END IF;
                  END IF;
                ELSE
                  SELECT "NormalizedPlateNumber" INTO canonical FROM "VisitorVehicles" WHERE "Id" = owner_id;
                  IF FOUND THEN
                    IF canonical IS NULL THEN
                      IF EXISTS (SELECT 1 FROM "VehiclePlateOwnership" WHERE "VisitorVehicleId" = owner_id) THEN
                        RAISE EXCEPTION 'Plate ownership must match its source visitor vehicle.' USING ERRCODE = '23514';
                      END IF;
                    ELSIF NOT EXISTS (SELECT 1 FROM "VehiclePlateOwnership" WHERE "VisitorVehicleId" = owner_id AND "NormalizedPlateNumber" = canonical) THEN
                      RAISE EXCEPTION 'Plate ownership must match its source visitor vehicle.' USING ERRCODE = '23514';
                    END IF;
                  END IF;
                END IF;
              END LOOP;
              RETURN NULL;
            END $validate$;

            DO $pin$
            DECLARE fn text;
            BEGIN
              FOREACH fn IN ARRAY ARRAY['MaintainVehiclePlateOwnership', 'ValidateVehiclePlateOwnership'] LOOP
                EXECUTE format('ALTER FUNCTION %I.%I(%s) SET search_path TO pg_catalog, %I', current_schema(), fn, '', current_schema());
              END LOOP;
            END $pin$;
            """;

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "PlateNumber",
                table: "Vehicles",
                type: "character varying(20)",
                maxLength: 20,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(20)",
                oldMaxLength: 20);

            migrationBuilder.AddCheckConstraint(
                name: "CK_Vehicles_PlateNumber_Or_ElectricBike",
                table: "Vehicles",
                sql: "\"VehicleType\" = 1 OR (\"PlateNumber\" IS NOT NULL AND length(btrim(\"PlateNumber\")) > 0)");

            migrationBuilder.Sql(UpdatePlateOwnershipTriggersSql);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_Vehicles_PlateNumber_Or_ElectricBike",
                table: "Vehicles");

            migrationBuilder.AlterColumn<string>(
                name: "PlateNumber",
                table: "Vehicles",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "character varying(20)",
                oldMaxLength: 20,
                oldNullable: true);
        }
    }
}
