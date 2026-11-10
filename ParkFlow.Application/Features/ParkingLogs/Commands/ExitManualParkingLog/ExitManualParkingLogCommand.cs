using MediatR;
using ParkFlow.Application.Common;
using ParkFlow.Application.Features.ParkingLogs.DTOs;

namespace ParkFlow.Application.Features.ParkingLogs.Commands.ExitManualParkingLog;

/// <param name="VehicleId">
/// Alternative to <paramref name="PlateNumber"/> for registered e-bikes, which have no plate.
/// Only accepted for plate-less electric bikes.
/// </param>
public record ExitManualParkingLogCommand(
    string PlateNumber = "",
    Guid? UserId = null,
    Guid? VehicleId = null
) : IRequest<Result<ExitParkingLogResponse>>;
