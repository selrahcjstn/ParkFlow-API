using ParkFlow.Domain.Entities;

namespace ParkFlow.Application.Interfaces;

public interface IParkingLogRepository
{
    Task AddParkingLogAsync(ParkingLog parkingLog);
    Task<ParkingLog?> GetActiveParkingLogByVehicleIdAsync(Guid vehicleId);
    Task UpdateParkingLogAsync(ParkingLog parkingLog);
}
