using MediatR;
using ParkFlow.Application.Common;
using ParkFlow.Application.Features.Reservations.DTOs;

using ParkFlow.Domain.Enums;

namespace ParkFlow.Application.Features.Reservations.Commands.CreateReservation;

public record CreateReservationCommand(
    Guid UserId,
    DateTime ReservationDate,
    TimeSpan StartTime,
    TimeSpan EndTime,
    string Reason,
    string? NotifyEmail = null,
    ReservationType Type = ReservationType.Normal,
    Guid? VehicleId = null) : IRequest<Result<ParkingReservationDto>>;
