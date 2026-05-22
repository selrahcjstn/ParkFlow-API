namespace ParkFlow.Application.Features.ParkingLogs.Services;

public record ParkingLogRoleDetails(
    string Role,
    string IdNumber,
    string? Course,
    int? YearLevel,
    string? Section,
    string? Department);