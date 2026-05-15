using MediatR;
using ParkFlow.Application.Common;

namespace ParkFlow.Application.Features.ParkingLogs.Commands.CreateParkingLog;

public record CreateParkingLogCommand(
    Guid VehicleId,
    Guid GuardId,
    DateTime EntryTime,
    DateTime? ExitTime
) : IRequest<Result<Guid>>;