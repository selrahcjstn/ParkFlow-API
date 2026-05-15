using MediatR;
using ParkFlow.Application.Common;

namespace ParkFlow.Application.Features.ParkingLogs.Commands.CreateParkingLog;

public record CreateParkingLogCommand(
    string QrCodeHash,
    Guid GuardId,
    DateTime EntryTime,
    DateTime? ExitTime
) : IRequest<Result<Guid>>;