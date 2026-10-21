START TRANSACTION;
ALTER TABLE "ParkingReservations" ADD "ReservedVehicleType" integer NOT NULL DEFAULT 0;

UPDATE "ParkingReservations" AS r
SET "ReservedVehicleType" = 2
FROM "Vehicles" AS v
WHERE r."VehicleId" = v."Id" AND v."VehicleType" = 2;

INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
VALUES ('20261021093833_SeparateReservationVehicleType', '10.0.7');

COMMIT;

