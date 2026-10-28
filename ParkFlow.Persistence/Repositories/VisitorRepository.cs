using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using ParkFlow.Application.Common;
using ParkFlow.Application.Interfaces;
using ParkFlow.Domain.Entities;
using ParkFlow.Application.Features.Visitors.DTOs;

namespace ParkFlow.Persistence.Repositories;

public class VisitorRepository(AppDbContext context) : IVisitorRepository
{
    private readonly AppDbContext _context = context;
    public Task<Visitor?> GetByIdAsync(Guid id) => _context.Visitors.FirstOrDefaultAsync(v => v.Id == id);
    public Task<VisitorVehicle?> GetVehicleByPlateNumberAsync(string plateNumber)
    {
        var normalized = VisitorVehicle.NormalizePlate(plateNumber);
        return _context.VisitorVehicles.Include(v => v.Visitor).SingleOrDefaultAsync(v => v.NormalizedPlateNumber == normalized);
    }
    public async Task<Visitor?> GetByPlateNumberAsync(string plateNumber) => (await GetVehicleByPlateNumberAsync(plateNumber))?.Visitor;
    public Task<Visitor?> GetWithSessionsByIdAsync(Guid id) => _context.Visitors
        .Include(v => v.Vehicles).Include(v => v.VisitSessions).ThenInclude(s => s.VisitorVehicle)
        .Include(v => v.VisitSessions).ThenInclude(s => s.EntryGuard!.UserProfile)
        .Include(v => v.VisitSessions).ThenInclude(s => s.ExitGuard!.UserProfile).FirstOrDefaultAsync(v => v.Id == id);
    public async Task<Visitor?> GetWithSessionsByPlateNumberAsync(string plateNumber)
    {
        var vehicle = await GetVehicleByPlateNumberAsync(plateNumber);
        return vehicle == null ? null : await GetWithSessionsByIdAsync(vehicle.VisitorId);
    }
    private static readonly Expression<Func<Visitor, VisitorDto>> Summary = v => new VisitorDto(
        v.Id, v.FullName, v.PlateNumber, v.Brand, (int)v.VehicleType, v.ContactNumber,
        v.VisitSessions.Max(s => (DateTime?)s.EntryTime), v.VisitSessions.Count,
        v.VisitSessions.Any(s => s.Status == VisitSessionStatus.Inside),
        v.Vehicles.OrderBy(x => x.CreatedAt).ThenBy(x => x.Id).Select(x => new VisitorVehicleDto(
            x.Id, x.VisitorId, x.PlateNumber, x.Brand, (int)x.VehicleType, x.VehiclePictureUrl,
            x.VisitSessions.Any(s => s.Status == VisitSessionStatus.Inside),
            x.VisitSessions.Where(s => s.Status == VisitSessionStatus.Inside).Select(s => (Guid?)s.Id).FirstOrDefault(),
            x.VisitSessions.Where(s => s.Status == VisitSessionStatus.Inside).Select(s => (DateTime?)s.EntryTime).FirstOrDefault(), x.VisitSessions.Count)).ToList());
    public async Task<(IEnumerable<VisitorDto> Items, int TotalCount)> GetPagedVisitorsAsync(int pageNumber, int pageSize, string? search, bool? onlyInside = null)
    {
        var query = _context.Visitors.AsNoTracking();
        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim().ToLowerInvariant();
            query = query.Where(v => v.FullName.ToLower().Contains(term) || (v.ContactNumber != null && v.ContactNumber.ToLower().Contains(term))
                || v.Vehicles.Any(x => x.PlateNumber.ToLower().Contains(term) || x.Brand.ToLower().Contains(term)));
        }
        if (onlyInside.HasValue) query = query.Where(v => v.VisitSessions.Any(s => s.Status == VisitSessionStatus.Inside) == onlyInside.Value);
        var count = await query.CountAsync();
        var items = await query.OrderByDescending(v => v.VisitSessions.Max(s => (DateTime?)s.EntryTime) ?? v.CreatedAt).ThenBy(v => v.Id)
            .Skip((pageNumber - 1) * pageSize).Take(pageSize).Select(Summary).ToListAsync();
        return (items, count);
    }
    public async Task<VisitorDetailDto?> GetDetailPageAsync(Guid id, int page, int pageSize, Guid? visitorVehicleId = null)
    {
        var visitor = await _context.Visitors.AsNoTracking().Where(v => v.Id == id).Select(Summary).FirstOrDefaultAsync();
        if (visitor == null) return null;
        var query = _context.VisitSessions.AsNoTracking().Where(s => s.VisitorId == id);
        if (visitorVehicleId.HasValue) query = query.Where(s => s.VisitorVehicleId == visitorVehicleId);
        var count = await query.CountAsync();
        var visits = await query.OrderByDescending(s => s.EntryTime).ThenByDescending(s => s.Id).Skip((page-1)*pageSize).Take(pageSize)
            .Select(s => new VisitSessionDto(s.Id, s.EntryTime, s.ExitTime, s.Purpose, s.Destination,
                s.EntryGuard != null ? s.EntryGuard.UserProfile.FirstName + " " + s.EntryGuard.UserProfile.LastName : null,
                s.ExitGuard != null ? s.ExitGuard.UserProfile.FirstName + " " + s.ExitGuard.UserProfile.LastName : null,
                s.EntryGate, s.ExitGate, s.Status == VisitSessionStatus.Inside ? "Inside" : s.Status == VisitSessionStatus.Cancelled ? "Cancelled" : "Completed",
                s.VisitorVehicleId, s.VisitorVehicle.PlateNumber, s.VisitorVehicle.Brand, (int)s.VisitorVehicle.VehicleType,
                s.DriversLicenseUrl, s.ProfilePictureUrl, s.VehiclePictureUrl)).ToListAsync();
        return new VisitorDetailDto(visitor, visits, page, pageSize, count);
    }
    public async Task AddAsync(Visitor visitor) { _context.Visitors.Add(visitor); await _context.SaveChangesAsync(); }
    public async Task UpdateAsync(Visitor visitor) { _context.Visitors.Update(visitor); await _context.SaveChangesAsync(); }
    public async Task DeleteAsync(Visitor visitor) { _context.Visitors.Remove(visitor); await _context.SaveChangesAsync(); }
    // Retained internal compatibility method: never pick an arbitrary session if several vehicles are inside.
    public async Task<VisitSession?> GetActiveVisitSessionByVisitorIdAsync(Guid visitorId)
    {
        var sessions = await _context.VisitSessions.Include(s => s.Visitor).Include(s => s.VisitorVehicle)
            .Where(s => s.VisitorId == visitorId && s.Status == VisitSessionStatus.Inside).Take(2).ToListAsync();
        return sessions.Count == 1 ? sessions[0] : null;
    }
    public Task<VisitSession?> GetActiveVisitSessionByVehicleIdAsync(Guid visitorVehicleId) => _context.VisitSessions
        .Include(s => s.Visitor).Include(s => s.VisitorVehicle)
        .SingleOrDefaultAsync(s => s.VisitorVehicleId == visitorVehicleId && s.Status == VisitSessionStatus.Inside);
    public async Task<VisitSession?> GetActiveVisitSessionByPlateNumberAsync(string plateNumber)
    {
        var vehicle = await GetVehicleByPlateNumberAsync(plateNumber);
        return vehicle == null ? null : await GetActiveVisitSessionByVehicleIdAsync(vehicle.Id);
    }
    public Task<VisitSession?> GetVisitSessionByIdAsync(Guid sessionId) => _context.VisitSessions.Include(s => s.Visitor)
        .Include(s => s.VisitorVehicle).FirstOrDefaultAsync(s => s.Id == sessionId);
    public async Task AddVisitSessionAsync(VisitSession session)
    {
        var vehicle = await _context.VisitorVehicles.Include(v => v.Visitor).SingleAsync(v => v.Id == session.VisitorVehicleId);
        await SaveEntryAsync(vehicle.Visitor, vehicle, session, false, false);
    }
    public async Task SaveEntryAsync(Visitor visitor, VisitorVehicle vehicle, VisitSession session, bool newVisitor, bool newVehicle, CancellationToken ct = default)
    {
        if (vehicle.VisitorId != visitor.Id || session.VisitorId != visitor.Id || session.VisitorVehicleId != vehicle.Id)
            throw new InvalidOperationException("The session and vehicle must belong to the selected visitor.");
        try
        {
            await _context.Database.CreateExecutionStrategy().ExecuteAsync(async () =>
            {
                await using var transaction = await _context.Database.BeginTransactionAsync(ct);
                var key = "VisitorVehicle:" + vehicle.NormalizedPlateNumber;
                await _context.Database.ExecuteSqlInterpolatedAsync($"SELECT pg_advisory_xact_lock(hashtextextended({key}, 0))", ct);
                if (await _context.VisitSessions.AnyAsync(s => s.Id == session.Id, ct)) { _context.ChangeTracker.AcceptAllChanges(); return; }
                var existingId = await _context.VisitorVehicles.Where(v => v.NormalizedPlateNumber == vehicle.NormalizedPlateNumber).Select(v => (Guid?)v.Id).SingleOrDefaultAsync(ct);
                if (existingId.HasValue && existingId != vehicle.Id) throw new ParkingCapacityException("This plate was registered by another request. Search the plate again.");
                if (await _context.Vehicles.AnyAsync(v => v.PlateNumber.Replace(" ", "").Replace("-", "").ToUpper() == vehicle.NormalizedPlateNumber, ct))
                    throw new ParkingCapacityException("This plate belongs to a registered vehicle. Search the plate again.");
                await ParkingCapacityGuard.EnsureSpaceAsync(_context, vehicle.VehicleType);
                if (await _context.VisitSessions.AnyAsync(s => s.VisitorVehicleId == vehicle.Id && s.Status == VisitSessionStatus.Inside, ct))
                    throw new ParkingCapacityException("This vehicle is already inside campus.");
                if (newVisitor) _context.Visitors.Add(visitor);
                if (newVehicle) _context.VisitorVehicles.Add(vehicle);
                _context.VisitSessions.Add(session);
                await _context.SaveChangesAsync(acceptAllChangesOnSuccess: false, ct);
                await transaction.CommitAsync(ct);
                _context.ChangeTracker.AcceptAllChanges();
            });
        }
        catch (DbUpdateException e) when (e.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation })
        { throw new ParkingCapacityException("This vehicle was registered or entered by another request. Search the plate again."); }
    }
    public async Task UpdateVisitSessionAsync(VisitSession session) { _context.VisitSessions.Update(session); await _context.SaveChangesAsync(); }
    public Task<int> GetActiveVisitSessionCountAsync() => _context.VisitSessions.CountAsync(s => s.Status == VisitSessionStatus.Inside);
    public async Task<bool> CompleteVisitSessionAsync(VisitSession session)
    {
        _context.Entry(session).Property(s => s.Status).OriginalValue = VisitSessionStatus.Inside;
        _context.Entry(session).State = EntityState.Modified;
        try { await _context.SaveChangesAsync(); return true; }
        catch (DbUpdateConcurrencyException) { _context.Entry(session).State = EntityState.Detached; return false; }
    }
}
