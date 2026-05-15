using ParkFlow.Domain.Entities;

namespace ParkFlow.Application.Interfaces;

public interface IVehicleRepository
{
    Task AddAsync(Vehicle vehicle);
    Task<Vehicle?> GetByIdAsync(Guid id);
    Task<Vehicle?> GetByQrCodeHashAsync(string qrCodeHash);
    Task<IEnumerable<Vehicle>> GetByOwnerIdAsync(Guid ownerId);
    Task UpdateAsync(Vehicle vehicle);
    Task DeleteAsync(Vehicle vehicle);
}
