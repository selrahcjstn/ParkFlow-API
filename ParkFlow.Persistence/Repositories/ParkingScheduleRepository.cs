using ParkFlow.Application.Interfaces;
using ParkFlow.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace ParkFlow.Persistence.Repositories;

public class ParkingScheduleRepository : IParkingScheduleRepository
{
    private readonly AppDbContext _appDbContext;

    public ParkingScheduleRepository(AppDbContext appDbContext)
    {
        _appDbContext = appDbContext;
    }

    public async Task AddAsync(ParkingSchedule parkingSchedule)
    {
        await _appDbContext.ParkingSchedules.AddAsync(parkingSchedule);
        await _appDbContext.SaveChangesAsync();
    }

    public async Task DeleteAsync(ParkingSchedule parkingSchedule)
    {
        _appDbContext.ParkingSchedules.Remove(parkingSchedule);
        await _appDbContext.SaveChangesAsync();
    }

    public Task<ParkingSchedule?> GetByIdAsync(Guid id)
    {
        return _appDbContext.ParkingSchedules
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == id);
    }

    public async Task<IEnumerable<ParkingSchedule>> GetBySubmissionIdAsync(Guid submissionId)
    {
        return await _appDbContext.ParkingSchedules
            .AsNoTracking()
            .Where(x => x.SubmissionId == submissionId)
            .ToListAsync();
    }

    public async Task UpdateAsync(ParkingSchedule parkingSchedule)
    {
        _appDbContext.ParkingSchedules.Update(parkingSchedule);
        await _appDbContext.SaveChangesAsync();
    }
}
