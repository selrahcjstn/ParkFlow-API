using FluentValidation;
using MediatR;
using ParkFlow.Application.Common;
using ParkFlow.Application.Features.ParkingLogs.DTOs;
using ParkFlow.Application.Features.ParkingLogs.Services;
using ParkFlow.Application.Interfaces;
using ParkFlow.Domain.Entities;
using ParkFlow.Domain.Enums;

namespace ParkFlow.Application.Features.ParkingLogs.Commands.ExitParkingLog;

public class ExitParkingLogHandler : IRequestHandler<ExitParkingLogCommand, Result<ExitParkingLogResponse>>
{
    private readonly IParkingLogRepository _parkingLogRepository;
    private readonly IVehicleRepository _vehicleRepository;
    private readonly IUserProfileRepository _userProfileRepository;
    private readonly IGuardRepository _guardRepository;
    private readonly ICorSubmissionRepository _corSubmissionRepository;
    private readonly IParkingScheduleRepository _parkingScheduleRepository;
    private readonly IStudentRepository _studentRepository;
    private readonly IPersonnelRepository _personnelRepository;
    private readonly IAdminRepository _adminRepository;
    private readonly IViolationRepository _violationRepository;
    private readonly IParkingService _parkingService;
    private readonly IViolationService _violationService;
    private readonly IParkingLogRoleService _parkingLogRoleService;
    private readonly IValidator<ExitParkingLogCommand> _validator;

    public ExitParkingLogHandler(
        IParkingLogRepository parkingLogRepository,
        IVehicleRepository vehicleRepository,
        IUserProfileRepository userProfileRepository,
        IGuardRepository guardRepository,
        ICorSubmissionRepository corSubmissionRepository,
        IParkingScheduleRepository parkingScheduleRepository,
        IStudentRepository studentRepository,
        IPersonnelRepository personnelRepository,
        IAdminRepository adminRepository,
        IViolationRepository violationRepository,
        IParkingService parkingService,
        IViolationService violationService,
        IParkingLogRoleService parkingLogRoleService,
        IValidator<ExitParkingLogCommand> validator)
    {
        _parkingLogRepository = parkingLogRepository;
        _vehicleRepository = vehicleRepository;
        _userProfileRepository = userProfileRepository;
        _guardRepository = guardRepository;
        _corSubmissionRepository = corSubmissionRepository;
        _parkingScheduleRepository = parkingScheduleRepository;
        _studentRepository = studentRepository;
        _personnelRepository = personnelRepository;
        _adminRepository = adminRepository;
        _violationRepository = violationRepository;
        _parkingService = parkingService;
        _violationService = violationService;
        _parkingLogRoleService = parkingLogRoleService;
        _validator = validator;
    }

    public async Task<Result<ExitParkingLogResponse>> Handle(ExitParkingLogCommand request, CancellationToken cancellationToken)
    {
        var validationResult = await _validator.ValidateAsync(request, cancellationToken);

        if (!validationResult.IsValid)
        {
            var errors = string.Join(", ", validationResult.Errors.Select(e => e.ErrorMessage));
            return Result<ExitParkingLogResponse>.Failure(errors, ErrorCode.BadRequest);
        }

        var vehicle = await _vehicleRepository.GetByQrCodeHashAsync(request.QrCodeHash);

        if (vehicle == null)
            return Result<ExitParkingLogResponse>.Failure("Invalid QR code. Vehicle not found.", ErrorCode.NotFound);

        var userProfile = await _userProfileRepository.GetByUserIdAsync(request.UserId);

        if (userProfile == null)
            return Result<ExitParkingLogResponse>.Failure("User profile not found.", ErrorCode.NotFound);

        var guard = await _guardRepository.GetByUserProfileIdAsync(userProfile.Id);

        if (guard == null)
            return Result<ExitParkingLogResponse>.Failure("Guard not found.", ErrorCode.NotFound);

        var active = await _parkingLogRepository.GetActiveParkingLogByVehicleIdAsync(vehicle.Id);

        if (active == null)
            return Result<ExitParkingLogResponse>.Failure("No active parking log found for this vehicle.", ErrorCode.NotFound);

        var statusBeforeExit = active.Status;
        var exitTime = DateTime.UtcNow;

        _parkingService.MarkExit(active);
        await _parkingLogRepository.UpdateParkingLogAsync(active);

        var corSubmissions = await _corSubmissionRepository.ListCorSubmissionsAsync();
        var verifiedCor = corSubmissions.FirstOrDefault(c => c.UserAccountId == vehicle.OwnerId && c.VerificationStatus == CorVerificationStatus.Verified);

        var endTime = exitTime;
        DateTime? maximumExitTime = null;
        double overstayTime = 0;
        decimal penaltyFee = 0m;
        bool isViolation = false;
        Guid? violationId = null;

        if (verifiedCor != null)
        {
            var schedules = await _parkingScheduleRepository.GetBySubmissionIdAsync(verifiedCor.Id);
            var philippinesNow = ParkingTimeHelper.ConvertUtcToPhilippinesTime(exitTime);
            var todaySchedule = schedules.FirstOrDefault(s => s.DayOfWeek == philippinesNow.DayOfWeek);

            if (todaySchedule != null)
            {
                endTime = ParkingTimeHelper.BuildPhilippinesScheduleUtcDateTime(philippinesNow, todaySchedule.EndTime);
                maximumExitTime = endTime.AddMinutes(30);

                if (_violationService.IsOverstay(exitTime, todaySchedule.EndTime))
                {
                    var overstayDuration = _violationService.GetOverstayDuration(exitTime, todaySchedule.EndTime);
                    overstayTime = overstayDuration.TotalHours;
                    penaltyFee = _violationService.CalculatePenalty(overstayDuration);

                    if (penaltyFee > 0m)
                    {
                        var recordedExitTime = active.ExitTime ?? exitTime;
                        var violation = new Violation(
                            active.Id,
                            ViolationType.Overstay,
                            penaltyFee,
                            statusBeforeExit,
                            active.EntryTime,
                            recordedExitTime,
                            (int)Math.Ceiling(overstayDuration.TotalMinutes));
                        await _violationRepository.AddAsync(violation);
                        isViolation = true;
                        violationId = violation.Id;
                    }
                }
            }
        }

        var actualExitTime = active.ExitTime ?? exitTime;

        var ownerProfile = await _userProfileRepository.GetByUserIdAsync(vehicle.OwnerId);

        if (ownerProfile == null)
            return Result<ExitParkingLogResponse>.Failure("Owner profile not found.", ErrorCode.NotFound);

        var student = await _studentRepository.GetByUserProfileIdAsync(ownerProfile.Id);
        var personnel = await _personnelRepository.GetByUserProfileIdAsync(ownerProfile.Id);
        var admin = await _adminRepository.GetByUserProfileIdAsync(ownerProfile.Id);

        var roleDetails = _parkingLogRoleService.GetRoleDetails(ownerProfile, student, personnel, admin);

        var response = new ExitParkingLogResponse
        {
            FirstName = ownerProfile.FirstName,
            LastName = ownerProfile.LastName,
            Role = roleDetails.Role,
            Status = active.Status.ToString(),
            PlateNumber = vehicle.PlateNumber,
            Brand = vehicle.Brand,
            VehicleType = vehicle.VehicleType.ToString(),
            EntryTime = active.EntryTime,
            ExitTime = actualExitTime,
            EndTime = endTime,
            OverstayTime = overstayTime,
            PenaltyFee = penaltyFee,
            IsViolation = isViolation,
            ViolationId = violationId
        };

        return Result<ExitParkingLogResponse>.Success(response, "Exit Confirmed");
    }
}
