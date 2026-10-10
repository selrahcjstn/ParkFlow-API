using ParkFlow.Domain.Entities;
using ParkFlow.Application.Features.Visitors.DTOs;

namespace ParkFlow.Application.Interfaces;

public interface IVisitorRepository
{
    Task<Visitor?> GetByIdAsync(Guid id);
    Task<Visitor?> GetByPlateNumberAsync(string plateNumber);
    Task<Visitor?> GetWithSessionsByIdAsync(Guid id);
    Task<Visitor?> GetWithSessionsByPlateNumberAsync(string plateNumber);
    Task<VisitorDetailDto?> GetDetailPageAsync(Guid id, int page, int pageSize);
    Task<(IEnumerable<VisitorDto> Items, int TotalCount)> GetPagedVisitorsAsync(int pageNumber, int pageSize, string? search, bool? onlyInside = null);
    Task AddAsync(Visitor visitor);
    Task UpdateAsync(Visitor visitor);
    Task DeleteAsync(Visitor visitor);

    Task<VisitSession?> GetActiveVisitSessionByVisitorIdAsync(Guid visitorId);
    Task<VisitSession?> GetActiveVisitSessionByPlateNumberAsync(string plateNumber);
    Task<VisitSession?> GetVisitSessionByIdAsync(Guid sessionId);
    Task AddVisitSessionAsync(VisitSession session);
    Task UpdateVisitSessionAsync(VisitSession session);
    Task<bool> CompleteVisitSessionWithChargeAsync(VisitSession session, Violation charge);
    Task<int> GetActiveVisitSessionCountAsync();
}
