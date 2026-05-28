using ParkFlow.Domain.Entities;

namespace ParkFlow.Application.Interfaces;

public interface IParkingLogRepository
{
    Task AddParkingLogAsync(ParkingLog parkingLog);
    Task<ParkingLog?> GetActiveParkingLogByVehicleIdAsync(Guid vehicleId);
    Task<IReadOnlyList<ParkingLog>> GetTodaysParkingLogsAsync(int limit);
    Task<IReadOnlyList<ParkingLog>> GetRecentParkingLogsAsync(int limit);
    Task<IReadOnlyList<ParkingLog>> GetParkingHistoryAsync(Guid? userId = null, int pageNumber = 1, int pageSize = 15);
    Task UpdateParkingLogAsync(ParkingLog parkingLog);
}
