using MediatR;

namespace ParkFlow.Application.Features.Schedules.Command;

public record CreateParkingScheduleCommand(
    Guid SubmissionId,
    DayOfWeek DayOfWeek,
    TimeSpan StartTime,
    TimeSpan EndTime) : IRequest<ParkFlow.Application.Common.Result<Guid>>;
    