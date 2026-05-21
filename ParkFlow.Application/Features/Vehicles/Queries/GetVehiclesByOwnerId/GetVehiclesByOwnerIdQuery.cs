using MediatR;
using ParkFlow.Application.Common;
using ParkFlow.Domain.Enums;

namespace ParkFlow.Application.Features.Vehicles.Queries.GetVehiclesByOwnerId;

public record GetVehiclesByOwnerIdQuery(Guid OwnerId) : IRequest<Result<IEnumerable<VehicleDto>>>;

public record VehicleDto(Guid Id, string PlateNumber, string Brand, string QrCodeHash, VehicleType VehicleType, string Status);
