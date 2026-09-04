using ParkFlow.Domain.Entities;
using ParkFlow.Domain.Enums;

namespace ParkFlow.Application.Interfaces;

public interface IParkingReservationRepository
{
    Task AddAsync(ParkingReservation reservation);
    Task<ParkingReservation?> GetByIdAsync(Guid id);
    Task<IEnumerable<ParkingReservation>> GetByUserIdAsync(Guid userId);
    Task<ParkingReservation?> GetByReferenceNumberAsync(string referenceNumber);
    Task<IEnumerable<ParkingReservation>> GetAllAsync(ReservationStatus? status = null);
    Task UpdateAsync(ParkingReservation reservation);
    Task SaveChangesAsync();
}
