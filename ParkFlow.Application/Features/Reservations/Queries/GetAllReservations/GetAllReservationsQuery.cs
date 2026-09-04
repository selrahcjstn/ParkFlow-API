using MediatR;
using ParkFlow.Application.Common;
using ParkFlow.Application.Features.Reservations.DTOs;
using ParkFlow.Domain.Enums;

namespace ParkFlow.Application.Features.Reservations.Queries.GetAllReservations;

public record GetAllReservationsQuery(ReservationStatus? Status = null) : IRequest<Result<IEnumerable<ParkingReservationDto>>>;
