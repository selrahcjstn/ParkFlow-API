namespace ParkFlow.Application.Features.ParkingLogs.DTOs;

public record ParkingLogsTodayResponse(
    int TotalCount,
    int Limit,
    IEnumerable<ParkingLogActivityDto> Items);