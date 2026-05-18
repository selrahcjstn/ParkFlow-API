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
    string PlateNumber,
    string Brand,
    DateTime EntryTime,
    DateTime? ExitTime);