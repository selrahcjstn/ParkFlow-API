using Microsoft.EntityFrameworkCore;
using ParkFlow.Application.Interfaces;
using ParkFlow.Domain.Entities;

namespace ParkFlow.Persistence.Repositories;

public class VisitorRepository : IVisitorRepository
{
    private readonly AppDbContext _context;

    public VisitorRepository(AppDbContext context)
    {
        _context = context;
    }

    public async Task<Visitor?> GetByIdAsync(Guid id)
    {
        return await _context.Visitors.FindAsync(id);
    }

    public async Task<Visitor?> GetByPlateNumberAsync(string plateNumber)
    {
        if (string.IsNullOrWhiteSpace(plateNumber)) return null;

        var normalized = plateNumber.Trim().ToUpper();
        var clean = normalized.Replace(" ", "").Replace("-", "");

        var visitor = await _context.Visitors
            .FirstOrDefaultAsync(v => v.PlateNumber.ToUpper() == normalized);

        if (visitor != null) return visitor;

        return await _context.Visitors
            .FirstOrDefaultAsync(v => v.PlateNumber.Replace(" ", "").Replace("-", "").ToUpper() == clean);
    }

    public async Task<Visitor?> GetWithSessionsByIdAsync(Guid id)
    {
        return await _context.Visitors
            .Include(v => v.VisitSessions.OrderByDescending(s => s.EntryTime))
                .ThenInclude(s => s.EntryGuard!.UserProfile)
            .Include(v => v.VisitSessions)
                .ThenInclude(s => s.ExitGuard!.UserProfile)
            .FirstOrDefaultAsync(v => v.Id == id);
    }

    public async Task<Visitor?> GetWithSessionsByPlateNumberAsync(string plateNumber)
    {
        if (string.IsNullOrWhiteSpace(plateNumber)) return null;

        var normalized = plateNumber.Trim().ToUpper();
        var clean = normalized.Replace(" ", "").Replace("-", "");

        var visitor = await _context.Visitors
            .Include(v => v.VisitSessions.OrderByDescending(s => s.EntryTime))
            .FirstOrDefaultAsync(v => v.PlateNumber.ToUpper() == normalized);

        if (visitor != null) return visitor;

        return await _context.Visitors
            .Include(v => v.VisitSessions.OrderByDescending(s => s.EntryTime))
            .FirstOrDefaultAsync(v => v.PlateNumber.Replace(" ", "").Replace("-", "").ToUpper() == clean);
    }

    public async Task<(IEnumerable<Visitor> Items, int TotalCount)> GetPagedVisitorsAsync(
        int pageNumber,
        int pageSize,
        string? search,
        bool? onlyInside = null)
    {
        var query = _context.Visitors
            .Include(v => v.VisitSessions)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim().ToLower();
            query = query.Where(v =>
                (v.FullName != null && v.FullName.ToLower().Contains(term)) ||
                v.PlateNumber.ToLower().Contains(term) ||
                v.Brand.ToLower().Contains(term) ||
                (v.ContactNumber != null && v.ContactNumber.ToLower().Contains(term)));
        }

        if (onlyInside.HasValue)
        {
            if (onlyInside.Value)
            {
                query = query.Where(v => v.VisitSessions.Any(s => s.Status == VisitSessionStatus.Inside));
            }
            else
            {
                query = query.Where(v => !v.VisitSessions.Any(s => s.Status == VisitSessionStatus.Inside));
            }
        }

        var totalCount = await query.CountAsync();

        var items = await query
            .OrderByDescending(v => v.VisitSessions.Max(s => (DateTime?)s.EntryTime) ?? v.CreatedAt)
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        return (items, totalCount);
    }

    public async Task AddAsync(Visitor visitor)
    {
        await _context.Visitors.AddAsync(visitor);
        await _context.SaveChangesAsync();
    }

    public async Task UpdateAsync(Visitor visitor)
    {
        _context.Visitors.Update(visitor);
        await _context.SaveChangesAsync();
    }

    public async Task DeleteAsync(Visitor visitor)
    {
        _context.Visitors.Remove(visitor);
        await _context.SaveChangesAsync();
    }

    public async Task<VisitSession?> GetActiveVisitSessionByVisitorIdAsync(Guid visitorId)
    {
        return await _context.VisitSessions
            .Include(s => s.Visitor)
            .FirstOrDefaultAsync(s => s.VisitorId == visitorId && s.Status == VisitSessionStatus.Inside);
    }

    public async Task<VisitSession?> GetActiveVisitSessionByPlateNumberAsync(string plateNumber)
    {
        var normalized = plateNumber.Trim().ToUpper();
        return await _context.VisitSessions
            .Include(s => s.Visitor)
            .FirstOrDefaultAsync(s => s.Visitor.PlateNumber.ToUpper() == normalized && s.Status == VisitSessionStatus.Inside);
    }

    public async Task<VisitSession?> GetVisitSessionByIdAsync(Guid sessionId)
    {
        return await _context.VisitSessions
            .Include(s => s.Visitor)
            .FirstOrDefaultAsync(s => s.Id == sessionId);
    }

    public async Task AddVisitSessionAsync(VisitSession session)
    {
        await _context.VisitSessions.AddAsync(session);
        await _context.SaveChangesAsync();
    }

    public async Task UpdateVisitSessionAsync(VisitSession session)
    {
        _context.VisitSessions.Update(session);
        await _context.SaveChangesAsync();
    }

    public async Task<int> GetActiveVisitSessionCountAsync()
    {
        return await _context.VisitSessions
            .CountAsync(s => s.Status == VisitSessionStatus.Inside);
    }
}
