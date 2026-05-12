using MediatR;
using ParkFlow.Application.Common;

namespace ParkFlow.Application.Features.Schedules.Command;

public class CreateParkingScheduleCommand : IRequest<Result<Guid>>
{
    public Guid SubmissionId { get; set; }
    public List<CreateParkingScheduleItem> Schedules { get; set; } = new();
}

public class CreateParkingScheduleItem
{
    public DayOfWeek DayOfWeek { get; set; }
    public TimeSpan StartTime { get; set; }
    public TimeSpan EndTime { get; set; }
}