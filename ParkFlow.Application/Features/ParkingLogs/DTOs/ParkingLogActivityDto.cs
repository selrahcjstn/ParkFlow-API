namespace ParkFlow.Application.Features.ParkingLogs.DTOs;

public record ParkingLogActivityDto(
    string FirstName,
    string LastName,
    string Role,
    string Status,
    string IdNumber,
    string Course,
    int YearLevel,
    string Section,
    string Department,
    string PlateNumber,
    string Brand,
    string EntryTime,
    string EntryDate);

public record ParkingLogHistoryDto(
    string ParkingLogId,
    string FirstName,
    string LastName,
    string PhoneNumber,
    string Status,
    string PlateNumber,
    string Brand,
    string VehicleType,
    string EntryTime,
    string? ExitTime,
    string SettlementStatus,
    decimal TotalFee,
    string MustExitBy,
    string TotalParkingHours
    );
