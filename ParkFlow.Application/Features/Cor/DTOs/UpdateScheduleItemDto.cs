namespace ParkFlow.Application.Features.Cor.DTOs;

public class UpdateScheduleItemDto
{
    public int DayOfWeek { get; set; }
    public string StartTime { get; set; } = string.Empty;
    public string EndTime { get; set; } = string.Empty;
}
