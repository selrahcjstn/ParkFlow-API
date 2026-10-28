using ParkFlow.Domain.Entities;
using ParkFlow.Application.Features.Visitors.DTOs;

namespace ParkFlow.Application.Interfaces;

public interface IVisitorRepository
{
    Task<VisitorVehicle?> GetVehicleByPlateNumberAsync(string plateNumber);
    Task<VisitSession?> GetActiveVisitSessionByVehicleIdAsync(Guid visitorVehicleId);
    Task SaveEntryAsync(Visitor visitor, VisitorVehicle vehicle, VisitSession session, bool newVisitor, bool newVehicle, CancellationToken ct = default);
    Task<Visitor?> GetByIdAsync(Guid id);
    Task<Visitor?> GetByPlateNumberAsync(string plateNumber);
    Task<Visitor?> GetWithSessionsByIdAsync(Guid id);
    Task<Visitor?> GetWithSessionsByPlateNumberAsync(string plateNumber);
    Task<VisitorDetailDto?> GetDetailPageAsync(Guid id, int page, int pageSize, Guid? visitorVehicleId = null);
    Task<(IEnumerable<VisitorDto> Items, int TotalCount)> GetPagedVisitorsAsync(int pageNumber, int pageSize, string? search, bool? onlyInside = null);
    Task AddAsync(Visitor visitor);
    Task UpdateAsync(Visitor visitor);
    Task DeleteAsync(Visitor visitor);

    Task<VisitSession?> GetActiveVisitSessionByVisitorIdAsync(Guid visitorId);
    Task<VisitSession?> GetActiveVisitSessionByPlateNumberAsync(string plateNumber);
    Task<VisitSession?> GetVisitSessionByIdAsync(Guid sessionId);
    Task AddVisitSessionAsync(VisitSession session);
    Task UpdateVisitSessionAsync(VisitSession session);
    Task<bool> CompleteVisitSessionAsync(VisitSession session);
    Task<int> GetActiveVisitSessionCountAsync();
}
