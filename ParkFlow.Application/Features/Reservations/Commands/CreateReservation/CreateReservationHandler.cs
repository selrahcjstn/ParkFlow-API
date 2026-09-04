using FluentValidation;
using MediatR;
using ParkFlow.Application.Common;
using ParkFlow.Application.Features.Reservations.DTOs;
using ParkFlow.Application.Interfaces;
using ParkFlow.Domain.Entities;

namespace ParkFlow.Application.Features.Reservations.Commands.CreateReservation;

public class CreateReservationHandler : IRequestHandler<CreateReservationCommand, Result<ParkingReservationDto>>
{
    private readonly IParkingReservationRepository _reservationRepository;
    private readonly IUserAccountRepository _userRepository;
    private readonly IValidator<CreateReservationCommand> _validator;

    public CreateReservationHandler(
        IParkingReservationRepository reservationRepository,
        IUserAccountRepository userRepository,
        IValidator<CreateReservationCommand> validator)
    {
        _reservationRepository = reservationRepository;
        _userRepository = userRepository;
        _validator = validator;
    }

    public async Task<Result<ParkingReservationDto>> Handle(CreateReservationCommand request, CancellationToken cancellationToken)
    {
        var validationResult = await _validator.ValidateAsync(request, cancellationToken);
        if (!validationResult.IsValid)
        {
            var errors = string.Join("; ", validationResult.Errors.Select(e => e.ErrorMessage));
            return Result<ParkingReservationDto>.Failure(errors, ErrorCode.BadRequest);
        }

        var user = await _userRepository.GetByIdAsync(request.UserId);
        if (user == null)
            return Result<ParkingReservationDto>.Failure("User not found.", ErrorCode.NotFound);

        // Generate Reference Number: RES-YYYYMMDD-XXXX
        var randomPart = new Random().Next(1000, 9999);
        var refNum = $"RES-{request.ReservationDate:yyyyMMdd}-{randomPart}";

        var reservation = new ParkingReservation(
            request.UserId,
            refNum,
            request.ReservationDate,
            request.StartTime,
            request.EndTime,
            request.Reason
        );

        await _reservationRepository.AddAsync(reservation);
        await _reservationRepository.SaveChangesAsync();

        var dto = new ParkingReservationDto
        {
            Id = reservation.Id,
            UserId = reservation.UserId,
            UserFullName = user.UserProfile != null ? $"{user.UserProfile.FirstName} {user.UserProfile.LastName}".Trim() : string.Empty,
            UserEmail = user.PrimaryEmail ?? string.Empty,
            ReferenceNumber = reservation.ReferenceNumber,
            ReservationDate = reservation.ReservationDate,
            StartTime = reservation.StartTime,
            EndTime = reservation.EndTime,
            Reason = reservation.Reason,
            Status = reservation.Status,
            AdminNotes = reservation.AdminNotes,
            ApprovedAt = reservation.ApprovedAt,
            ApprovedByAdminId = reservation.ApprovedByAdminId,
            CreatedAt = reservation.CreatedAt
        };

        return Result<ParkingReservationDto>.Success(dto, "Parking reservation created successfully.");
    }
}
