using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using ParkFlow.Application.Common;
using ParkFlow.Application.Interfaces;
using ParkFlow.Domain.Entities;

namespace ParkFlow.Persistence.Repositories;

public class SystemSettingsRepository(AppDbContext db) : ISystemSettingsRepository
{
    private static readonly SemaphoreSlim Gate = new(1, 1);
    public async Task LoadAsync(CancellationToken ct = default)
    {
        var saved = await db.Set<SystemSettingsDocument>().AsNoTracking().SingleOrDefaultAsync(s => s.Id == 1, ct);
        if (saved != null)
            SystemSettingsStore.Replace(JsonSerializer.Deserialize<SystemSettingsDto>(saved.Json, new JsonSerializerOptions { PropertyNameCaseInsensitive = true })
                ?? throw new InvalidOperationException("Saved system settings are invalid."));
    }
    public async Task SaveAsync(SystemSettingsDto settings, CancellationToken ct = default)
    {
        await Gate.WaitAsync(ct);
        try
        {
            await db.Database.CreateExecutionStrategy().ExecuteAsync(async () =>
            {
                await using var transaction = await db.Database.BeginTransactionAsync(ct);
                foreach (var type in new[] { ParkFlow.Domain.Enums.VehicleType.Car, ParkFlow.Domain.Enums.VehicleType.Motorcycle })
                {
                    var key = "ParkingCapacity:" + type;
                    await db.Database.ExecuteSqlInterpolatedAsync($"SELECT pg_advisory_xact_lock(hashtextextended({key}, 0))", ct);
                }
                var occupied = await ParkingCapacityGuard.CountAsync(db);
                if (settings.CarParking.TotalCapacity < occupied.Cars || settings.MotorcycleParking.TotalCapacity < occupied.Motorcycles)
                    throw new ParkingCapacityException("Capacity cannot be lower than the number of vehicles currently inside. Refresh and try again.");
                var saved = await db.Set<SystemSettingsDocument>().SingleOrDefaultAsync(s => s.Id == 1, ct);
                if (saved == null) { saved = new(); db.Add(saved); }
                saved.Json = JsonSerializer.Serialize(settings);
                saved.UpdatedAt = DateTime.UtcNow;
                await db.SaveChangesAsync(ct);
                await transaction.CommitAsync(ct);
                SystemSettingsStore.Replace(settings);
            });
        }
        finally { Gate.Release(); }
    }
}
