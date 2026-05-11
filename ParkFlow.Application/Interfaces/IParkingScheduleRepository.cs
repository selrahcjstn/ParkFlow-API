using ParkFlow.Domain.Entities;

namespace ParkFlow.Application.Interfaces;

public interface IParkingScheduleRepository
{
    Task AddAsync(ParkingSchedule parkingSchedule);
    Task<ParkingSchedule?> GetByIdAsync(Guid id);
    Task<IEnumerable<ParkingSchedule>> GetBySubmissionIdAsync(Guid submissionId);
    Task UpdateAsync(ParkingSchedule parkingSchedule);
    Task DeleteAsync(ParkingSchedule parkingSchedule);
}
