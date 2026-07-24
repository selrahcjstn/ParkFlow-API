using MediatR;
using ParkFlow.Application.Common;
using ParkFlow.Domain.Enums;

namespace ParkFlow.Application.Features.Vehicles.Queries.GetAllVehicles;

public record AdminVehicleDto(
    Guid Id,
    Guid OwnerId,
    string OwnerName,
    string OwnerEmail,
    string OwnerRole,
    string PlateNumber,
    string Brand,
    string QrCodeHash,
    VehicleType VehicleType,
    string Status,
    bool IsPrimary
);

public record GetAllVehiclesQuery() : IRequest<Result<IEnumerable<AdminVehicleDto>>>;
