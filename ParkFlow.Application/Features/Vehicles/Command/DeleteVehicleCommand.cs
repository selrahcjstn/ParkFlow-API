using MediatR;
using ParkFlow.Application.Common;

namespace ParkFlow.Application.Features.Vehicles.Command;

public record DeleteVehicleCommand(
    Guid VehicleId,
    Guid OwnerId,
    bool IsAdmin = false
) : IRequest<Result<Guid>>;
