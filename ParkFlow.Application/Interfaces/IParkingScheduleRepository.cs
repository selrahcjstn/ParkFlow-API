using ParkFlow.Domain.Entities;

namespace ParkFlow.Application.Interfaces;

public interface IParkingScheduleRepository
{
    Task AddAsync(ParkingSchedule parkingSchedule);
    Task<ParkingSchedule?> GetByIdAsync(Guid id);
    Task<IEnumerable<ParkingSchedule>> GetBySubmissionIdAsync(Guid submissionId);
    Task<IEnumerable<ParkingSchedule>> GetByUserIdAsync(Guid userId);
    Task UpdateAsync(ParkingSchedule parkingSchedule);
    Task DeleteAsync(ParkingSchedule parkingSchedule);
    Task ReplaceSchedulesAsync(Guid submissionId, IEnumerable<ParkingSchedule> newSchedules);
}
