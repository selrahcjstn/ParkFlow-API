namespace ParkFlow.Application.Interfaces;

public interface IParkingLogHistoryRepository
{
    Task AddAsync(ParkingLogHistory history);
    Task<List<ParkingLogHistory>> GetByVehicleIdAsync(Guid vehicleId);
}   