using ParkFlow.Domain.Enums;

namespace ParkFlow.Application.Features.ParkingLogs.DTOs;

public record ParkingLogActivityDto(
    Guid LogId,
    string PlateNumber,
    DateTime Date,
    DateTime CreatedAt,
    string Brand,
    VehicleType VehicleType,
    string TransactionType,
    DateTime EntryTime,
    DateTime? ExitTime);