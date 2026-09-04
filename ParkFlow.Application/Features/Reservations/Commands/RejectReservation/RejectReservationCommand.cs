using MediatR;
using ParkFlow.Application.Common;

namespace ParkFlow.Application.Features.Reservations.Commands.RejectReservation;

public record RejectReservationCommand(Guid ReservationId, Guid AdminId, string? Notes) : IRequest<Result<bool>>;
