using MediatR;
using ParkFlow.Application.Common;
using ParkFlow.Application.Features.ParkingLogs.DTOs;

namespace ParkFlow.Application.Features.ParkingLogs.Commands.CreateParkingLog;

public record CreateParkingLogCommand(
    string QrCodeHash,
    Guid UserId
) : IRequest<Result<CreateParkingLogResponse>>;