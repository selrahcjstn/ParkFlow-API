using MediatR;
using ParkFlow.Application.Common;

namespace ParkFlow.Application.Features.Reservations.Commands.ApproveReservation;

public record ApproveReservationCommand(Guid ReservationId, Guid AdminId, string? Notes) : IRequest<Result<bool>>;
