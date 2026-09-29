using FluentValidation;
using MediatR;
using ParkFlow.Application.Common;
using ParkFlow.Application.Features.Reservations.DTOs;
using ParkFlow.Application.Interfaces;
using ParkFlow.Domain.Entities;
using ParkFlow.Domain.Enums;

namespace ParkFlow.Application.Features.Reservations.Commands.CreateReservation;

public class CreateReservationHandler : IRequestHandler<CreateReservationCommand, Result<ParkingReservationDto>>
{
    private readonly IParkingReservationRepository _reservationRepository;
    private readonly IUserAccountRepository _userRepository;
    private readonly IVehicleRepository _vehicleRepository;
    private readonly IValidator<CreateReservationCommand> _validator;
    private readonly ISignalRNotificationSender _notificationSender;
    private readonly IEmailService _emailService;
    private readonly ICorSubmissionRepository? _corSubmissionRepository;
    private readonly IViolationRepository? _violationRepository;

    public CreateReservationHandler(
        IParkingReservationRepository reservationRepository,
        IUserAccountRepository userRepository,
        IVehicleRepository vehicleRepository,
        IValidator<CreateReservationCommand> validator,
        ISignalRNotificationSender notificationSender,
        IEmailService emailService,
        ICorSubmissionRepository? corSubmissionRepository = null,
        IViolationRepository? violationRepository = null)
    {
        _reservationRepository = reservationRepository;
        _userRepository = userRepository;
        _vehicleRepository = vehicleRepository;
        _validator = validator;
        _notificationSender = notificationSender;
        _emailService = emailService;
        _corSubmissionRepository = corSubmissionRepository;
        _violationRepository = violationRepository;
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

        if (_violationRepository != null && await _violationRepository.HasActiveViolationByUserIdAsync(user.Id))
        {
            return Result<ParkingReservationDto>.Failure(
                "You have active/unpaid violations. Please settle pending charges before creating a reservation.",
                ErrorCode.Forbidden);
        }

        var isAccountActive = user.Status == AccountStatus.Active;

        // 1. Check if user is an Admin or Guard
        var isAdmin = user.UserProfile?.Admin != null;
        var isGuard = user.UserProfile?.Guard != null;

        // 2. Check COR verification status
        CorSubmission? latestCor = null;
        if (_corSubmissionRepository != null)
        {
            latestCor = await _corSubmissionRepository.GetLatestByUserIdAsync(user.Id);
            if (latestCor != null && latestCor.VerificationStatus == CorVerificationStatus.Verified)
            {
                isAccountActive = true;
                if (user.Status != AccountStatus.Active)
                {
                    user.Verify();
                    await _userRepository.UpdateAsync(user);
                }
            }
            else if (latestCor != null && latestCor.VerificationStatus != CorVerificationStatus.Verified)
            {
                isAccountActive = false;
            }
        }

        // 3. Check Vehicle verification status
        var userVehicles = (await _vehicleRepository.GetByOwnerIdAsync(user.Id)).ToList();
        if (userVehicles.Any(v => v.VerificationStatus == CorVerificationStatus.Verified))
        {
            // If user has a verified vehicle and COR is not explicitly rejected, user is verified
            if (latestCor == null || latestCor.VerificationStatus == CorVerificationStatus.Verified)
            {
                isAccountActive = true;
                if (user.Status != AccountStatus.Active)
                {
                    user.Verify();
                    await _userRepository.UpdateAsync(user);
                }
            }
        }

        if (isAdmin || isGuard)
        {
            isAccountActive = true;
        }

        if (!isAccountActive)
        {
            return Result<ParkingReservationDto>.Failure(
                "Your account must be verified and approved by an administrator before you can create parking reservations.",
                ErrorCode.Forbidden);
        }

        // Find vehicle to bind (user's primary vehicle if vehicleId not specified)
        Guid? assignedVehicleId = request.VehicleId;
        Vehicle? assignedVehicle = null;

        if (assignedVehicleId.HasValue)
        {
            assignedVehicle = await _vehicleRepository.GetByIdAsync(assignedVehicleId.Value);
        }
        else
        {
            assignedVehicle = userVehicles.FirstOrDefault(v => v.IsPrimary) ?? userVehicles.FirstOrDefault();
            assignedVehicleId = assignedVehicle?.Id;
        }

        // Generate Reference Number: RES-YYYYMMDD-XXXX
        var randomPart = new Random().Next(1000, 9999);
        var refNum = $"RES-{request.ReservationDate:yyyyMMdd}-{randomPart}";

        var endTimeToUse = request.Type == ReservationType.Special
            ? new TimeSpan(23, 59, 59)
            : request.EndTime;

        var reservation = new ParkingReservation(
            request.UserId,
            refNum,
            request.ReservationDate,
            request.StartTime,
            endTimeToUse,
            request.Reason,
            request.Type,
            assignedVehicleId
        );

        if (!string.IsNullOrWhiteSpace(request.NotifyEmail))
        {
            reservation.SetAdminNotes($"[NotifyEmail:{request.NotifyEmail.Trim()}]");
        }

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
            Type = reservation.Type,
            VehicleId = reservation.VehicleId,
            PlateNumber = assignedVehicle?.PlateNumber,
            Brand = assignedVehicle?.Brand,
            VehicleQrCodeHash = assignedVehicle?.QrCodeHash,
            AdminNotes = reservation.AdminNotes,
            ApprovedAt = reservation.ApprovedAt,
            ApprovedByAdminId = reservation.ApprovedByAdminId,
            CreatedAt = reservation.CreatedAt
        };

        try
        {
            var eventData = new
            {
                id = reservation.Id,
                referenceNumber = reservation.ReferenceNumber,
                userId = reservation.UserId,
                userFullName = dto.UserFullName,
                plateNumber = dto.PlateNumber,
                reservationDate = reservation.ReservationDate,
                startTime = reservation.StartTime,
                endTime = reservation.EndTime,
                reason = reservation.Reason,
                status = reservation.Status.ToString(),
                type = reservation.Type.ToString(),
                createdAt = reservation.CreatedAt
            };

            await _notificationSender.SendToAllAsync("ReservationSubmitted", eventData);
            await _notificationSender.SendToAllAsync("ReservationUpdated", dto);
            await _notificationSender.SendToAllAsync("ApprovalListUpdated", new
            {
                type = "Reservation",
                id = reservation.Id,
                referenceNumber = reservation.ReferenceNumber,
                status = reservation.Status.ToString()
            });
        }
        catch
        {
            // Ignore SignalR broadcast error to prevent request failure
        }

        return Result<ParkingReservationDto>.Success(dto, "Parking reservation created successfully.");
    }
}
