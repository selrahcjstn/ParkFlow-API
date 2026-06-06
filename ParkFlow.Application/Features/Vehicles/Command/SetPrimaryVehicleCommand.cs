using MediatR;
using ParkFlow.Application.Common;

namespace ParkFlow.Application.Features.Vehicles.Command;

public record SetPrimaryVehicleCommand(
    Guid VehicleId,
    Guid OwnerId
) : IRequest<Result<Guid>>;
