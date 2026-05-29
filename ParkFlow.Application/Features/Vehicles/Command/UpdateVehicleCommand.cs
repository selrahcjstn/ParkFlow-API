using MediatR;
using ParkFlow.Application.Common;
using ParkFlow.Domain.Enums;

namespace ParkFlow.Application.Features.Vehicles.Command;

public record UpdateVehicleCommand(
    Guid VehicleId,
    Guid OwnerId,
    string PlateNumber,
    string Brand,
    VehicleType VehicleType
) : IRequest<Result<Guid>>;
