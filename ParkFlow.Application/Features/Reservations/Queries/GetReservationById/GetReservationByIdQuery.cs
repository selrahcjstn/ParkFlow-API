using MediatR;
using ParkFlow.Application.Common;
using ParkFlow.Application.Features.Reservations.DTOs;

namespace ParkFlow.Application.Features.Reservations.Queries.GetReservationById;

public record GetReservationByIdQuery(Guid Id) : IRequest<Result<ParkingReservationDto>>;
