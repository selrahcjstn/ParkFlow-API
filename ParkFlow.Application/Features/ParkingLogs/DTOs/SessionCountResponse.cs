namespace ParkFlow.Application.Features.ParkingLogs.DTOs;

public record SessionCountResponse(
    int ActiveSessionCount,
    int OverstayCount,
    int MaximumCapacity,
    int ManualSessionCount);