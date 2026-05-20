namespace ParkFlow.Application.Features.ParkingLogs.DTOs;

public record GetActiveParkingSessionResult(
    IEnumerable<GetActiveParkingSessionResponse> Items,
    int Count,
    int Limit);
