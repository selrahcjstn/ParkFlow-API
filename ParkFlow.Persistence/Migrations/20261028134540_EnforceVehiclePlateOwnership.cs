using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ParkFlow.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class EnforceVehiclePlateOwnership : Migration
    {

        public const string PreflightSql = """
            LOCK TABLE "Vehicles", "VisitorVehicles" IN SHARE ROW EXCLUSIVE MODE;
            CREATE FUNCTION "CanonicalVehiclePlate"(text) RETURNS text LANGUAGE sql IMMUTABLE STRICT PARALLEL SAFE AS $canonical$
              SELECT upper(replace(replace(btrim($1, U&'\0009\000A\000B\000C\000D\0020\0085\00A0\1680\2000\2001\2002\2003\2004\2005\2006\2007\2008\2009\200A\2028\2029\202F\205F\3000'), ' ', ''), '-', ''))
            $canonical$;
            DO $preflight$
            BEGIN
              IF EXISTS (SELECT plate FROM (
                  SELECT "CanonicalVehiclePlate"("PlateNumber") AS plate FROM "Vehicles"
                  UNION ALL SELECT "NormalizedPlateNumber" FROM "VisitorVehicles") all_plates
                  GROUP BY plate HAVING count(*) > 1 OR length(plate) > 20) THEN
                RAISE EXCEPTION 'Plate ownership backfill blocked: conflicting canonical plates or an unsupported canonical length. Reconcile explicitly; no vehicles or identities have been merged.';
              END IF;
            END $preflight$;
            """;

        public const string AuthoritySql = """
            INSERT INTO "VehiclePlateOwnership" ("NormalizedPlateNumber", "VehicleId", "VisitorVehicleId")
              SELECT "CanonicalVehiclePlate"("PlateNumber"), "Id", NULL::uuid FROM "Vehicles"
              UNION ALL SELECT "NormalizedPlateNumber", NULL::uuid, "Id" FROM "VisitorVehicles";

            CREATE FUNCTION "MaintainVehiclePlateOwnership"() RETURNS trigger LANGUAGE plpgsql AS $maintain$
            DECLARE canonical text;
            BEGIN
              canonical := "CanonicalVehiclePlate"(NEW."PlateNumber");
              IF TG_OP = 'INSERT' THEN
                IF TG_TABLE_NAME = 'Vehicles' THEN
                  INSERT INTO "VehiclePlateOwnership" VALUES (canonical, NEW."Id", NULL);
                ELSE
                  INSERT INTO "VehiclePlateOwnership" VALUES (canonical, NULL, NEW."Id");
                END IF;
              ELSE
                IF TG_TABLE_NAME = 'Vehicles' THEN
                  UPDATE "VehiclePlateOwnership" SET "NormalizedPlateNumber" = canonical WHERE "VehicleId" = NEW."Id";
                ELSE
                  UPDATE "VehiclePlateOwnership" SET "NormalizedPlateNumber" = canonical WHERE "VisitorVehicleId" = NEW."Id";
                END IF;
                IF NOT FOUND THEN RAISE EXCEPTION 'Plate ownership authority is missing; stop and inspect integrity.' USING ERRCODE = '23514'; END IF;
              END IF;
              RETURN NEW;
            END $maintain$;

            CREATE FUNCTION "ValidateVehiclePlateOwnership"() RETURNS trigger LANGUAGE plpgsql AS $validate$
            DECLARE canonical text; owner_id uuid; registered boolean; iteration integer;
            BEGIN
              -- Validate final transaction state, including multiple plate edits or
              -- insert-then-remove in one transaction, rather than stale trigger images.
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
                  IF FOUND AND NOT EXISTS (SELECT 1 FROM "VehiclePlateOwnership" WHERE "VehicleId" = owner_id AND "NormalizedPlateNumber" = canonical) THEN
                    RAISE EXCEPTION 'Plate ownership must match its source vehicle.' USING ERRCODE = '23514';
                  END IF;
                ELSE
                  SELECT "NormalizedPlateNumber" INTO canonical FROM "VisitorVehicles" WHERE "Id" = owner_id;
                  IF FOUND AND NOT EXISTS (SELECT 1 FROM "VehiclePlateOwnership" WHERE "VisitorVehicleId" = owner_id AND "NormalizedPlateNumber" = canonical) THEN
                    RAISE EXCEPTION 'Plate ownership must match its source visitor vehicle.' USING ERRCODE = '23514';
                  END IF;
                END IF;
              END LOOP;
              RETURN NULL;
            END $validate$;
            CREATE FUNCTION "PreventPlateOwnershipTruncate"() RETURNS trigger LANGUAGE plpgsql AS $truncate$
            BEGIN RAISE EXCEPTION 'Plate ownership cannot be truncated; use a reviewed integrity recovery plan.'; END $truncate$;

            -- Pin function lookup to this migration's schema, not a caller-controlled search_path.
            DO $pin$
            DECLARE fn text;
            BEGIN
              FOREACH fn IN ARRAY ARRAY['CanonicalVehiclePlate', 'MaintainVehiclePlateOwnership', 'ValidateVehiclePlateOwnership', 'PreventPlateOwnershipTruncate'] LOOP
                EXECUTE format('ALTER FUNCTION %I.%I(%s) SET search_path TO pg_catalog, %I', current_schema(), fn,
                  CASE WHEN fn = 'CanonicalVehiclePlate' THEN 'text' ELSE '' END, current_schema());
              END LOOP;
            END $pin$;
            CREATE TRIGGER "TR_Vehicles_PlateOwnership" AFTER INSERT OR UPDATE OF "PlateNumber" ON "Vehicles"
              FOR EACH ROW EXECUTE FUNCTION "MaintainVehiclePlateOwnership"();
            CREATE TRIGGER "TR_VisitorVehicles_PlateOwnership" AFTER INSERT OR UPDATE OF "PlateNumber", "NormalizedPlateNumber" ON "VisitorVehicles"
              FOR EACH ROW EXECUTE FUNCTION "MaintainVehiclePlateOwnership"();
            CREATE CONSTRAINT TRIGGER "TR_PlateOwnership_Source" AFTER INSERT OR UPDATE OR DELETE ON "VehiclePlateOwnership"
              DEFERRABLE INITIALLY DEFERRED FOR EACH ROW EXECUTE FUNCTION "ValidateVehiclePlateOwnership"();
            CREATE TRIGGER "TR_PlateOwnership_NoTruncate" BEFORE TRUNCATE ON "VehiclePlateOwnership"
              FOR EACH STATEMENT EXECUTE FUNCTION "PreventPlateOwnershipTruncate"();
            """;

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(PreflightSql);
            migrationBuilder.CreateTable(
                name: "VehiclePlateOwnership",
                columns: table => new
                {
                    NormalizedPlateNumber = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    VehicleId = table.Column<Guid>(type: "uuid", nullable: true),
                    VisitorVehicleId = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_VehiclePlateOwnership", x => x.NormalizedPlateNumber);
                    table.CheckConstraint("CK_VehiclePlateOwnerships_OneOwner", "(\"VehicleId\" IS NOT NULL) <> (\"VisitorVehicleId\" IS NOT NULL)");
                    table.ForeignKey(
                        name: "FK_VehiclePlateOwnership_Vehicles_VehicleId",
                        column: x => x.VehicleId,
                        principalTable: "Vehicles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_VehiclePlateOwnership_VisitorVehicles_VisitorVehicleId",
                        column: x => x.VisitorVehicleId,
                        principalTable: "VisitorVehicles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_VehiclePlateOwnership_VehicleId",
                table: "VehiclePlateOwnership",
                column: "VehicleId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_VehiclePlateOwnership_VisitorVehicleId",
                table: "VehiclePlateOwnership",
                column: "VisitorVehicleId",
                unique: true);
                    migrationBuilder.Sql(AuthoritySql);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            throw new NotSupportedException("Removing plate ownership authority requires a separately reviewed compatibility/recovery plan. No history is discarded automatically.");
        }
    }
}
