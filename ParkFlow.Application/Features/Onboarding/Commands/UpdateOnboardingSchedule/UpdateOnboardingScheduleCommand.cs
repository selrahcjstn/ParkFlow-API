using MediatR;
using ParkFlow.Application.Common;

namespace ParkFlow.Application.Features.Onboarding.Commands.UpdateOnboardingSchedule;

public record UpdateOnboardingScheduleCommand(
    Guid UserId,
    List<ScheduleItem> Items) : IRequest<Result<Guid>>;

public record ScheduleItem(DayOfWeek DayOfWeek, TimeSpan StartTime, TimeSpan EndTime);
