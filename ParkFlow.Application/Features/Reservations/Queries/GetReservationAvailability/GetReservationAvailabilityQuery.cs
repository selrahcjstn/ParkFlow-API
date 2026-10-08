using MediatR;
using ParkFlow.Application.Common;
using ParkFlow.Application.Interfaces;
using ParkFlow.Domain.Enums;

namespace ParkFlow.Application.Features.Reservations.Queries.GetReservationAvailability;

public record GetReservationAvailabilityQuery(DateTime Date, TimeSpan StartTime, TimeSpan EndTime,
    ReservationType Type = ReservationType.Normal) : IRequest<Result<ReservationAvailability>>;

public class GetReservationAvailabilityHandler(IParkingReservationRepository repository)
    : IRequestHandler<GetReservationAvailabilityQuery, Result<ReservationAvailability>>
{
    public async Task<Result<ReservationAvailability>> Handle(GetReservationAvailabilityQuery request, CancellationToken cancellationToken)
    {
        if (request.Date == default || !Enum.IsDefined(request.Type) || request.StartTime < TimeSpan.Zero
            || request.EndTime > TimeSpan.FromDays(1) || request.StartTime >= request.EndTime)
            return Result<ReservationAvailability>.Failure("Choose a valid date and time range.", ErrorCode.BadRequest);
        var start = request.Type == ReservationType.Special ? TimeSpan.Zero : request.StartTime;
        var end = request.Type == ReservationType.Special ? TimeSpan.FromDays(1) : request.EndTime;
        var booked = await repository.GetReservedPeakAsync(request.Date, start, end, cancellationToken);
        return Result<ReservationAvailability>.Success(ReservationCapacityPolicy.Describe(SystemSettingsStore.Current, booked));
    }
}
