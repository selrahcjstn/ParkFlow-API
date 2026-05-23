using MediatR;
using ParkFlow.Application.Common;
using ParkFlow.Application.Features.Schedules.DTOs;

namespace ParkFlow.Application.Features.Schedules.Queries.GetParkingSchedule;

public record GetParkingScheduleQuery(Guid UserId) : IRequest<Result<IEnumerable<ParkingScheduleResponseDto>>>;
