using Microsoft.EntityFrameworkCore;
using ParkFlow.Application.Common;
using ParkFlow.Domain.Entities;
using ParkFlow.Domain.Enums;

namespace ParkFlow.Persistence.Repositories;

internal static class ParkingCapacityGuard
{
    internal static async Task<(int Cars, int Motorcycles)> CountAsync(AppDbContext db)
    {
        var parked = db.ParkingLogs.Where(p => p.Status == ParkingStatus.Parked && p.ExitTime == null);
        var visits = db.VisitSessions.Where(s => s.Status == VisitSessionStatus.Inside);
        var cars = await parked.CountAsync(p => p.Vehicle.VehicleType == VehicleType.Car)
            + await visits.CountAsync(s => s.VisitorVehicle.VehicleType == VehicleType.Car);
        var motorcycles = await parked.CountAsync(p => p.Vehicle.VehicleType != VehicleType.Car)
            + await visits.CountAsync(s => s.VisitorVehicle.VehicleType != VehicleType.Car);
        return (cars, motorcycles);
    }

    internal static async Task EnsureSpaceAsync(AppDbContext db, VehicleType type)
    {
        var group = type == VehicleType.Car ? VehicleType.Car : VehicleType.Motorcycle;
        var key = "ParkingCapacity:" + group;
        await db.Database.ExecuteSqlInterpolatedAsync($"SELECT pg_advisory_xact_lock(hashtextextended({key}, 0))");
        var counts = await CountAsync(db);
        var occupied = group == VehicleType.Car ? counts.Cars : counts.Motorcycles;
        var capacity = SystemSettingsStore.Current.ForVehicle(group).TotalCapacity;
        if (occupied >= capacity)
            throw new ParkingCapacityException($"No {group.ToString().ToLowerInvariant()} parking spaces are available.");
    }
}
