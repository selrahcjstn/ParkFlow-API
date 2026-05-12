using MediatR;
using ParkFlow.Application.Common;

namespace ParkFlow.Application.Features.Vehicles.Command;

public record CreateVehicleCommand(
    Guid OwnerId,
    string PlateNumber,
    string Brand
) : IRequest<Result<Guid>>;
