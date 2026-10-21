using Microsoft.EntityFrameworkCore;
using ParkFlow.Application.Interfaces;
using ParkFlow.Domain.Entities;

namespace ParkFlow.Persistence.Repositories;

public class ParkingLogRepository : IParkingLogRepository
{
    private readonly AppDbContext _context;
    public ParkingLogRepository(AppDbContext context)
    {
        _context = context;
    }

    public Task<(int Cars, int Motorcycles)> GetOccupancyAsync() => ParkingCapacityGuard.CountAsync(_context);

    public async Task AddParkingLogAsync(ParkingLog parkingLog)
    {
        await _context.Database.CreateExecutionStrategy().ExecuteAsync(async () =>
        {
            await using var transaction = await _context.Database.BeginTransactionAsync();
            var type = await _context.Vehicles.Where(v => v.Id == parkingLog.VehicleId).Select(v => v.VehicleType).SingleAsync();
            if (await _context.ParkingLogs.AnyAsync(p => p.Id == parkingLog.Id))
            {
                _context.ChangeTracker.AcceptAllChanges();
                return;
            }
            var vehicle = await _context.Vehicles.AsNoTracking().SingleAsync(v => v.Id == parkingLog.VehicleId);
            var accountKey = "Account" + vehicle.OwnerId;
            await _context.Database.ExecuteSqlInterpolatedAsync($"SELECT pg_advisory_xact_lock(hashtextextended({accountKey}, 0))");
            vehicle = await _context.Vehicles.AsNoTracking().SingleAsync(v => v.Id == parkingLog.VehicleId);
            var driver = await _context.UserProfiles.Include(p => p.Admin).AsNoTracking().SingleAsync(p => p.UserAccountId == vehicle.OwnerId);
            if (driver.Admin == null && vehicle.VerificationStatus != ParkFlow.Domain.Enums.CorVerificationStatus.Verified)
                throw new ParkFlow.Application.Common.ParkingCapacityException("Vehicle documents require admin approval before entry.");
            var restriction = ParkFlow.Application.Features.ParkingLogs.Services.DriverEntryPolicy.MissingRequirement(driver);
            if (restriction != null) throw new ParkFlow.Application.Common.ParkingCapacityException(restriction);
            await ParkingCapacityGuard.EnsureSpaceAsync(_context, type);
            if (await _context.ParkingLogs.AnyAsync(p => p.VehicleId == parkingLog.VehicleId && p.Status == ParkingStatus.Parked && p.ExitTime == null))
                throw new ParkFlow.Application.Common.ParkingCapacityException("This vehicle is already inside campus.");
            await _context.Set<ParkingLog>().AddAsync(parkingLog);
            await _context.SaveChangesAsync(acceptAllChangesOnSuccess: false);
            await transaction.CommitAsync();
            _context.ChangeTracker.AcceptAllChanges();
        });
    }

    public async Task<ParkingLog?> GetActiveParkingLogByVehicleIdAsync(Guid vehicleId)
    {
        return await _context.Set<ParkingLog>()
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.VehicleId == vehicleId && p.Status == ParkingStatus.Parked && p.ExitTime == null);
    }

    public async Task<bool> HasActiveParkingLogByUserIdAsync(Guid userId)
    {
        return await _context.Set<ParkingLog>()
            .AsNoTracking()
            .AnyAsync(p => p.Vehicle.OwnerId == userId && p.Status == ParkingStatus.Parked && p.ExitTime == null);
    }

    public async Task<IReadOnlyList<ParkingLog>> GetActiveParkingLogsAsync(int limit)
    {
        return await _context.Set<ParkingLog>()
            .AsNoTracking()
            .Include(p => p.Vehicle)
                .ThenInclude(v => v.Owner)
                    .ThenInclude(o => o.AuthIdentities)
            .Include(p => p.Vehicle)
                .ThenInclude(v => v.Owner)
                    .ThenInclude(o => o.UserProfile!)
                        .ThenInclude(up => up!.Student)
            .Include(p => p.Vehicle)
                .ThenInclude(v => v.Owner)
                    .ThenInclude(o => o.UserProfile!)
                        .ThenInclude(up => up!.Personnel)
            .Include(p => p.Vehicle)
                .ThenInclude(v => v.Owner)
                    .ThenInclude(o => o.UserProfile!)
                        .ThenInclude(up => up!.Guard)
            .Where(p => p.Status == ParkingStatus.Parked && p.ExitTime == null)
            .OrderByDescending(p => p.CreatedAt)
            .Take(limit)
            .ToListAsync();
    }

    public async Task<IReadOnlyList<ParkingLog>> GetTodaysParkingLogsAsync(int limit)
    {
        var today = DateTime.UtcNow.Date;

        var query = _context.Set<ParkingLog>()
            .AsNoTracking()
            .Include(p => p.Vehicle)
                .ThenInclude(v => v.Owner)
                    .ThenInclude(o => o.AuthIdentities)
            .Include(p => p.Vehicle)
                .ThenInclude(v => v.Owner)
                    .ThenInclude(o => o.UserProfile!)
                        .ThenInclude(up => up!.Student)
            .Include(p => p.Vehicle)
                .ThenInclude(v => v.Owner)
                    .ThenInclude(o => o.UserProfile!)
                        .ThenInclude(up => up!.Personnel)
            .Include(p => p.Vehicle)
                .ThenInclude(v => v.Owner)
                    .ThenInclude(o => o.UserProfile!)
                        .ThenInclude(up => up!.Guard)
            .Where(p => p.EntryTime.Date == today && p.Status == ParkingStatus.Parked && p.ExitTime == null)
            .OrderByDescending(p => p.CreatedAt);

        return await query.Take(limit).ToListAsync();
    }

    public async Task<IReadOnlyList<ParkingLog>> GetRecentParkingLogsAsync(int limit)
    {
        return await _context.Set<ParkingLog>()
            .AsNoTracking()
            .Include(p => p.Vehicle)
                .ThenInclude(v => v.Owner)
                    .ThenInclude(o => o.AuthIdentities)
            .Include(p => p.Vehicle)
                .ThenInclude(v => v.Owner)
                    .ThenInclude(o => o.UserProfile!)
                        .ThenInclude(up => up!.Student)
            .Include(p => p.Vehicle)
                .ThenInclude(v => v.Owner)
                    .ThenInclude(o => o.UserProfile!)
                        .ThenInclude(up => up!.Personnel)
            .Include(p => p.Vehicle)
                .ThenInclude(v => v.Owner)
                    .ThenInclude(o => o.UserProfile!)
                        .ThenInclude(up => up!.Guard)
            .OrderByDescending(p => p.CreatedAt)
            .Take(limit)
            .ToListAsync();
    }

    public async Task<IReadOnlyList<ParkingLog>> GetParkingHistoryAsync(Guid? userId = null, int pageNumber = 1, int pageSize = 15)
    {
        var query = _context.Set<ParkingLog>()
            .AsNoTracking()
            .Where(p => p.Status == ParkingStatus.Exited || p.ExitTime != null)
            .Include(p => p.Vehicle)
                .ThenInclude(v => v.Owner)
                    .ThenInclude(o => o.AuthIdentities)
            .Include(p => p.Vehicle)
                .ThenInclude(v => v.Owner)
                    .ThenInclude(o => o.UserProfile!)
                        .ThenInclude(up => up!.Student)
            .Include(p => p.Vehicle)
                .ThenInclude(v => v.Owner)
                    .ThenInclude(o => o.UserProfile!)
                        .ThenInclude(up => up!.Personnel)
            .Include(p => p.Vehicle)
                .ThenInclude(v => v.Owner)
                    .ThenInclude(o => o.UserProfile!)
                        .ThenInclude(up => up!.Guard)
            .Include(p => p.Guard)
                .ThenInclude(g => g!.UserProfile)
            .AsQueryable();

        if (userId.HasValue)
        {
            query = query.Where(p => p.Vehicle.OwnerId == userId.Value);
        }

        return await query
            .OrderByDescending(p => p.ExitTime ?? p.EntryTime)
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();
    }

    public async Task<bool> CompleteExitAsync(ParkingLog log, Violation? charge, IReadOnlyCollection<ParkingReservation> reservations, CancellationToken ct = default)
    {
        return await _context.Database.CreateExecutionStrategy().ExecuteAsync(async () =>
        {
            await using var transaction = await _context.Database.BeginTransactionAsync(ct);
            var key = "ParkingExit:" + log.Id;
            await _context.Database.ExecuteSqlInterpolatedAsync($"SELECT pg_advisory_xact_lock(hashtextextended({key}, 0))", ct);
            var saved = await _context.ParkingLogs.AsNoTracking().SingleAsync(p => p.Id == log.Id, ct);
            if (saved.ExitTime != null)
            {
                // A retried commit may already have succeeded. Never insert another charge.
                var sameExit = (saved.ExitTime.HasValue && log.ExitTime.HasValue && Math.Abs((saved.ExitTime.Value - log.ExitTime.Value).TotalMilliseconds) < 1) || (charge != null && await _context.Violations.AnyAsync(v => v.Id == charge.Id, ct));
                _context.ChangeTracker.Clear();
                return sameExit;
            }
            var exit = _context.Entry(log);
            if (exit.State == EntityState.Detached) exit.State = EntityState.Unchanged;
            exit.Property(p => p.ExitTime).IsModified = true;
            exit.Property(p => p.Status).IsModified = true;
            exit.Property(p => p.UpdatedAt).IsModified = true;
            if (charge != null) _context.Violations.Add(charge);
            foreach (var reservation in reservations)
            {
                // Complete only the booking; do not mark its loaded account/profile graph modified.
                var booking = _context.Entry(reservation);
                if (booking.State == EntityState.Detached) booking.State = EntityState.Unchanged;
                booking.Property(r => r.Status).IsModified = true;
                booking.Property(r => r.UpdatedAt).IsModified = true;
            }
            await _context.SaveChangesAsync(acceptAllChangesOnSuccess: false, cancellationToken: ct);
            await transaction.CommitAsync(ct);
            _context.ChangeTracker.AcceptAllChanges();
            return true;
        });
    }

    public async Task UpdateParkingLogAsync(ParkingLog parkingLog)
    {
        _context.Set<ParkingLog>().Update(parkingLog);
        await _context.SaveChangesAsync();
    }
}
