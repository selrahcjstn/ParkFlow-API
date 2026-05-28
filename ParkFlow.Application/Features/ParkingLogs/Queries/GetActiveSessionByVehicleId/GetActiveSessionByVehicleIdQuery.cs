using MediatR;
using ParkFlow.Application.Common;
using ParkFlow.Application.Features.ParkingLogs.DTOs;

namespace ParkFlow.Application.Features.ParkingLogs.Queries.GetActiveSessionByVehicleId;

public record GetActiveSessionByVehicleIdQuery(
    Guid VehicleId
) : IRequest<Result<ActiveParkingSessionResponse>>;
