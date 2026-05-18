using MediatR;
using ParkFlow.Application.Common;
using ParkFlow.Application.Interfaces;
using ParkFlow.Application.Features.ParkingLogs.DTOs;
using ParkFlow.Domain.Entities;
using ParkFlow.Domain.Enums;

namespace ParkFlow.Application.Features.ParkingLogs.Commands.ExitParkingLog;

public class ExitParkingLogHandler : IRequestHandler<ExitParkingLogCommand, Result<ExitParkingLogResponse>>
{
    private readonly IVehicleRepository _vehicleRepository;
    private readonly IParkingLogRepository _parkingLogRepository;
    private readonly IUserProfileRepository _userProfileRepository;
    private readonly IGuardRepository _guardRepository;
    private readonly ICorSubmissionRepository _corSubmissionRepository;
    private readonly IParkingScheduleRepository _parkingScheduleRepository;
    private readonly IViolationRepository _violationRepository;

    public ExitParkingLogHandler(
        IVehicleRepository vehicleRepository,
        IParkingLogRepository parkingLogRepository,
        IUserProfileRepository userProfileRepository,
        IGuardRepository guardRepository,
        ICorSubmissionRepository corSubmissionRepository,
        IParkingScheduleRepository parkingScheduleRepository,
        IViolationRepository violationRepository)
    {
        _vehicleRepository = vehicleRepository;
        _parkingLogRepository = parkingLogRepository;
        _userProfileRepository = userProfileRepository;
        _guardRepository = guardRepository;
        _corSubmissionRepository = corSubmissionRepository;
        _parkingScheduleRepository = parkingScheduleRepository;
        _violationRepository = violationRepository;
    }

    public async Task<Result<ExitParkingLogResponse>> Handle(ExitParkingLogCommand request, CancellationToken cancellationToken)
    {
        var vehicle = await _vehicleRepository.GetByQrCodeHashAsync(request.QrCodeHash);

        if (vehicle == null)
            return Result<ExitParkingLogResponse>.Failure("Vehicle not found.", ErrorCode.NotFound);

        var userProfile = await _userProfileRepository.GetByUserIdAsync(request.UserId);
        if (userProfile == null)
            return Result<ExitParkingLogResponse>.Failure("User profile not found.", ErrorCode.NotFound);

        var guard = await _guardRepository.GetByUserProfileIdAsync(userProfile.Id);
        if (guard == null)
            return Result<ExitParkingLogResponse>.Failure("Guard not found.", ErrorCode.NotFound);

        var active = await _parkingLogRepository.GetActiveParkingLogByVehicleIdAsync(vehicle.Id);
        if (active == null)
            return Result<ExitParkingLogResponse>.Failure("No active parking log found.", ErrorCode.NotFound);

            // Assign server timestamp for exit
            var exitTime = DateTime.UtcNow;

            // Set exit on parking log (entity handles timestamp)
            active.Exit();
            await _parkingLogRepository.UpdateParkingLogAsync(active);

            // Check schedule for overstay
            var corSubmissions = await _corSubmissionRepository.ListCorSubmissionsAsync();
            var verifiedCor = corSubmissions.FirstOrDefault(c => c.UserAccountId == vehicle.OwnerId && c.VerificationStatus == CorVerificationStatus.Verified);

        bool isViolation = false;
        decimal penaltyFee = 0m;
        Guid? violationId = null;

        if (verifiedCor != null)
        {
            var schedules = await _parkingScheduleRepository.GetBySubmissionIdAsync(verifiedCor.Id);
            var todaySchedule = schedules.FirstOrDefault(s => s.DayOfWeek == exitTime.DayOfWeek);
            if (todaySchedule != null)
            {
                var exitTimeOfDay = exitTime.TimeOfDay;
                if (exitTimeOfDay > todaySchedule.EndTime)
                {
                    isViolation = true;
                    var over = exitTimeOfDay - todaySchedule.EndTime;
                    var hours = Math.Ceiling(over.TotalHours);
                    penaltyFee = (decimal)hours * 5m;

                    var violation = new Violation(active.Id, ViolationType.Overstay, penaltyFee);
                    await _violationRepository.AddAsync(violation);
                    violationId = violation.Id;
                }
            }
        }

        var response = new ExitParkingLogResponse
        {
            PlateNumber = vehicle.PlateNumber,
            ExitTime = exitTime,
            IsViolation = isViolation,
            PenaltyFee = penaltyFee,
            ViolationId = violationId
        };

        return Result<ExitParkingLogResponse>.Success(response, "Exit processed.");
    }
}
