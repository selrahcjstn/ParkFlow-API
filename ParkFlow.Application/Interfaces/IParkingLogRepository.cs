using ParkFlow.Domain.Entities;

namespace ParkFlow.Application.Interfaces;

public interface IParkingLogRepository
{
    async Task<(int Cars, int Motorcycles)> GetOccupancyAsync()
    {
        var logs = await GetActiveParkingLogsAsync(int.MaxValue);
        return (logs.Count(p => p.Vehicle?.VehicleType == ParkFlow.Domain.Enums.VehicleType.Car),
            logs.Count(p => p.Vehicle?.VehicleType != ParkFlow.Domain.Enums.VehicleType.Car));
    }
    Task AddParkingLogAsync(ParkingLog parkingLog);
    Task<ParkingLog?> GetActiveParkingLogByVehicleIdAsync(Guid vehicleId);
    Task<bool> HasActiveParkingLogByUserIdAsync(Guid userId);
    Task<IReadOnlyList<ParkingLog>> GetActiveParkingLogsAsync(int limit);
    Task<IReadOnlyList<ParkingLog>> GetTodaysParkingLogsAsync(int limit);
    Task<IReadOnlyList<ParkingLog>> GetRecentParkingLogsAsync(int limit);
    Task<IReadOnlyList<ParkingLog>> GetParkingHistoryAsync(Guid? userId = null, int pageNumber = 1, int pageSize = 15);
    Task UpdateParkingLogAsync(ParkingLog parkingLog);
}
