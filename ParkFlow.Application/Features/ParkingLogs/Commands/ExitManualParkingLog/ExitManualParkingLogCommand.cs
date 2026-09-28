using MediatR;
using ParkFlow.Application.Common;
using ParkFlow.Application.Features.ParkingLogs.DTOs;

namespace ParkFlow.Application.Features.ParkingLogs.Commands.ExitManualParkingLog;

public record ExitManualParkingLogCommand(
    string PlateNumber,
    Guid? UserId = null
) : IRequest<Result<ExitParkingLogResponse>>;
