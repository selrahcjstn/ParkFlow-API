using MediatR;
using ParkFlow.Application.Common;
using ParkFlow.Application.Features.ParkingLogs.DTOs;

namespace ParkFlow.Application.Features.ParkingLogs.Commands.CreateParkingLog;

public record CreateParkingLogCommand(
    string QrCodeHash,
    Guid userId,
    DateTime EntryTime
) : IRequest<Result<CreateParkingLogResponse>>;