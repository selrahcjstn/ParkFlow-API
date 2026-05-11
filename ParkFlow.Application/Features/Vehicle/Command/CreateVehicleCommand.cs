using MediatR;
using ParkFlow.Application.Common;

namespace ParkFlow.Application.Features.Vehicle.Command;

public record CreateVehicleCommand(
    Guid OwnerId,
    string PlateNumber,
    string Brand
) : IRequest<Result<Guid>>;
