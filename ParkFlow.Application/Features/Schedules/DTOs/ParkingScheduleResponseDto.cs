using System;

namespace ParkFlow.Application.Features.Schedules.DTOs;

public class ParkingScheduleResponseDto
{
    public string AcademicTerm { get; set; } = null!;
    public DayOfWeek DayOfWeek { get; set; }
    public TimeSpan StartTime { get; set; }
    public TimeSpan EndTime { get; set; }
}
