using MediatR;
using ParkFlow.Application.Common;
using ParkFlow.Application.Features.ParkingLogs.DTOs;
using ParkFlow.Domain.Enums;

namespace ParkFlow.Application.Features.ParkingLogs.Commands.CreateManualParkingLog;

public record CreateManualParkingLogCommand(
    string PlateNumber,
    VehicleType VehicleType,
    string? PhoneNumber,
    string? Brand,
    Guid UserId,
    // Registered e-bikes have no plate, so a lookup by name, student/employee number or phone returns the
    // vehicle id; entry must use it instead of the "EBIKE" placeholder shown in place of a plate.
    Guid? VehicleId = null
): IRequest<Result<CreateParkingLogResponse>>;
