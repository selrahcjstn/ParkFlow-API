namespace ParkFlow.Application.Features.ParkingLogs.DTOs;

public record SessionCountResponse(
    int ActiveSessionCount,
    int OverstayCount,
    int MaximumCapacity,
    int ManualSessionCount,
    int CarCount = 0, int MotorcycleCount = 0, int CarCapacity = 0, int MotorcycleCapacity = 0);