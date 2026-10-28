using Microsoft.EntityFrameworkCore;
using ParkFlow.Application.Common;
using ParkFlow.Domain.Entities;

namespace ParkFlow.Persistence.Repositories;

internal static class VehiclePlateGuard
{
    internal static async Task EnsureRegisteredVehicleAsync(AppDbContext db, Vehicle vehicle)
    {
        // Match VisitorVehicle's boundary trimming before storing new/edited input.
        // Historical rows are not rewritten.
        vehicle.Update(vehicle.PlateNumber.Trim(), vehicle.Brand, vehicle.VehicleType, vehicle.QrCodeHash);
        var plate = VisitorVehicle.NormalizePlate(vehicle.PlateNumber);
        // Same transaction-scoped key as visitor entry. Both writers must cooperate.
        var key = "VisitorVehicle:" + plate;
        await db.Database.ExecuteSqlInterpolatedAsync($"SELECT pg_advisory_xact_lock(hashtextextended({key}, 0))");
        if (await db.VisitorVehicles.AnyAsync(v => v.NormalizedPlateNumber == plate))
            throw new VehiclePlateConflictException("This plate is associated with a visitor vehicle. Contact an administrator to review ownership; no records have been transferred or merged.");
        if (await db.Vehicles.AnyAsync(v => v.Id != vehicle.Id && v.PlateNumber.Replace(" ", "").Replace("-", "").ToUpper() == plate))
            throw new VehiclePlateConflictException("This plate number is already registered. Refresh your vehicles before trying again.");
    }
}
