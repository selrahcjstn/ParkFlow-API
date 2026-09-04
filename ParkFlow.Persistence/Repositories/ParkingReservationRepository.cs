using Microsoft.EntityFrameworkCore;
using ParkFlow.Application.Interfaces;
using ParkFlow.Domain.Entities;
using ParkFlow.Domain.Enums;

namespace ParkFlow.Persistence.Repositories;

public class ParkingReservationRepository : IParkingReservationRepository
{
    private readonly AppDbContext _context;

    public ParkingReservationRepository(AppDbContext context)
    {
        _context = context;
    }

    public async Task AddAsync(ParkingReservation reservation)
    {
        await _context.ParkingReservations.AddAsync(reservation);
    }

    public async Task<ParkingReservation?> GetByIdAsync(Guid id)
    {
        return await _context.ParkingReservations
            .Include(r => r.UserAccount)
                .ThenInclude(u => u.UserProfile)
            .Include(r => r.UserAccount)
                .ThenInclude(u => u.AuthIdentities)
            .FirstOrDefaultAsync(r => r.Id == id);
    }

    public async Task<IEnumerable<ParkingReservation>> GetByUserIdAsync(Guid userId)
    {
        return await _context.ParkingReservations
            .Include(r => r.UserAccount)
                .ThenInclude(u => u.UserProfile)
            .Include(r => r.UserAccount)
                .ThenInclude(u => u.AuthIdentities)
            .Where(r => r.UserId == userId)
            .OrderByDescending(r => r.CreatedAt)
            .ToListAsync();
    }

    public async Task<ParkingReservation?> GetByReferenceNumberAsync(string referenceNumber)
    {
        return await _context.ParkingReservations
            .Include(r => r.UserAccount)
                .ThenInclude(u => u.UserProfile)
            .Include(r => r.UserAccount)
                .ThenInclude(u => u.AuthIdentities)
            .FirstOrDefaultAsync(r => r.ReferenceNumber == referenceNumber);
    }

    public async Task<IEnumerable<ParkingReservation>> GetAllAsync(ReservationStatus? status = null)
    {
        var query = _context.ParkingReservations
            .Include(r => r.UserAccount)
                .ThenInclude(u => u.UserProfile)
            .Include(r => r.UserAccount)
                .ThenInclude(u => u.AuthIdentities)
            .AsQueryable();

        if (status.HasValue)
        {
            query = query.Where(r => r.Status == status.Value);
        }

        return await query
            .OrderByDescending(r => r.CreatedAt)
            .ToListAsync();
    }

    public Task UpdateAsync(ParkingReservation reservation)
    {
        _context.ParkingReservations.Update(reservation);
        return Task.CompletedTask;
    }

    public async Task SaveChangesAsync()
    {
        await _context.SaveChangesAsync();
    }
}
