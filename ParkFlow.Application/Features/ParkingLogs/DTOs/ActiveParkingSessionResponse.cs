using System;

namespace ParkFlow.Application.Features.ParkingLogs.DTOs;

public class ActiveParkingSessionResponse
{
    public string SessionId { get; set; } = null!;
    public string StartedAt { get; set; } = null!;
    public int ElapsedMinutes { get; set; }
    public decimal AccruedCharge { get; set; }
    public string ExitBy { get; set; } = null!;
}
