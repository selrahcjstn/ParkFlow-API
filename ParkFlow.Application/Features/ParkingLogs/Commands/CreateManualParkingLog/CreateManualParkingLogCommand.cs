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
    Guid UserId
) : IRequest<Result<CreateParkingLogResponse>>;
