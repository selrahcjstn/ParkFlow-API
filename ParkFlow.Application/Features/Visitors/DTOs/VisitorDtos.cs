using ParkFlow.Domain.Enums;

namespace ParkFlow.Application.Features.Visitors.DTOs;

public record VisitorDto(
    Guid Id,
    string FullName,
    string PlateNumber,
    string Brand,
    int VehicleType,
    string? ContactNumber,
    DateTime? LastVisit,
    int TotalVisits,
    bool IsInside
);

public record VisitSessionDto(
    Guid Id,
    DateTime EntryTime,
    DateTime? ExitTime,
    string? Purpose,
    string? Destination,
    string? EntryGuardName,
    string? ExitGuardName,
    string? EntryGate,
    string? ExitGate,
    string Status
);

public record VisitorDetailDto(
    VisitorDto Visitor,
    IEnumerable<VisitSessionDto> Visits,
    int Page = 1,
    int PageSize = 20,
    int TotalCount = 0
);

public record PagedVisitorsResponse(
    IEnumerable<VisitorDto> Items,
    int Page,
    int PageSize,
    int TotalCount,
    bool HasMore
);

public record CreateVisitorEntryCommand(
    string PlateNumber,
    string? FullName,
    string? ContactNumber,
    VehicleType VehicleType,
    string Brand,
    string? Purpose = null,
    string? Destination = null,
    Guid? GuardUserId = null,
    string? EntryGate = null
) : MediatR.IRequest<Common.Result<VisitorEntryResponse>>;

public record VisitorEntryResponse(
    Guid VisitorId,
    Guid SessionId,
    string FullName,
    string PlateNumber,
    string Brand,
    string VehicleType,
    DateTime EntryTime,
    string? Purpose,
    string? Destination,
    string Status,
    bool IsReturning
);

public record ExitVisitorSessionCommand(
    string PlateNumber,
    Guid? GuardUserId,
    string? ExitGate = null
) : MediatR.IRequest<Common.Result<VisitorExitResponse>>;

public record VisitorExitResponse(
    Guid VisitorId,
    Guid SessionId,
    string FullName,
    string PlateNumber,
    string Brand,
    string VehicleType,
    DateTime EntryTime,
    DateTime ExitTime,
    string? Purpose,
    string? Destination,
    string Status
);

public record UnifiedPlateLookupDto(
    string LookupType, // "Registered", "Visitor", "Unknown"
    bool IsRegistered,
    bool IsVisitor,
    bool IsInside,
    string PlateNumber,
    // Registered vehicle info
    Guid? VehicleId = null,
    Guid? OwnerId = null,
    string? OwnerName = null,
    string? OwnerRole = null,
    // Visitor info
    Guid? VisitorId = null,
    string? FullName = null,
    string? ContactNumber = null,
    // Common vehicle info
    string? Brand = null,
    string? VehicleType = null,
    int? VehicleTypeValue = null,
    // Active session info
    Guid? ActiveSessionId = null,
    DateTime? EntryTime = null,
    string? Purpose = null,
    string? Destination = null,
    string? EntryMethod = null
);
