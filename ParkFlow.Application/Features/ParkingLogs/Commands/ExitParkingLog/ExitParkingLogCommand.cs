using MediatR;
using ParkFlow.Application.Common;
using ParkFlow.Application.Features.ParkingLogs.DTOs;

namespace ParkFlow.Application.Features.ParkingLogs.Commands.ExitParkingLog;

public record ExitParkingLogCommand(
    string QrCodeHash,
    Guid UserId
) : IRequest<Result<ExitParkingLogResponse>>;