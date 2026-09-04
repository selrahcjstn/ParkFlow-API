using MediatR;
using ParkFlow.Application.Common;
using ParkFlow.Application.Features.Reservations.DTOs;

namespace ParkFlow.Application.Features.Reservations.Queries.GetMyReservations;

public record GetMyReservationsQuery(Guid UserId) : IRequest<Result<IEnumerable<ParkingReservationDto>>>;
