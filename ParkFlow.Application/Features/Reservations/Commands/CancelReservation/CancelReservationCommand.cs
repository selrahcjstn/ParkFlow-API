using MediatR;
using ParkFlow.Application.Common;

namespace ParkFlow.Application.Features.Reservations.Commands.CancelReservation;

public record CancelReservationCommand(Guid ReservationId, Guid UserId) : IRequest<Result<bool>>;
