using ParkFlow.Domain.Entities;

namespace ParkFlow.Application.Interfaces;

public interface IParkingLogRepository
{
    Task AddParkingLogAsync(ParkingLog parkingLog);
    Task<ParkingLog?> GetActiveParkingLogByVehicleIdAsync(Guid vehicleId);
    Task<IReadOnlyList<ParkingLog>> GetTodaysParkingLogsAsync(int limit);
    Task<IReadOnlyList<ParkingLog>> GetRecentParkingLogsAsync(int limit);
    Task UpdateParkingLogAsync(ParkingLog parkingLog);
}
