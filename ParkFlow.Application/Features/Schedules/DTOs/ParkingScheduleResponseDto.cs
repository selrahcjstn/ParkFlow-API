using System;

namespace ParkFlow.Application.Features.Schedules.DTOs;

public class ParkingScheduleResponseDto
{
    public string AcademicTerm { get; set; } = null!;
    public DayOfWeek DayOfWeek { get; set; }
    public DateTime StartTime { get; set; }
    public DateTime EndTime { get; set; }
}
