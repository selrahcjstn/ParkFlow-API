using MediatR;
using ParkFlow.Application.Common;
using ParkFlow.Application.Interfaces;
using ParkFlow.Application.Features.ParkingLogs.DTOs;
using ParkFlow.Application.Features.ParkingLogs.Services;
using ParkFlow.Domain.Entities;
using ParkFlow.Domain.Enums;

namespace ParkFlow.Application.Features.ParkingLogs.Commands.CreateManualParkingLog;

public class CreateManualParkingLogHandler : IRequestHandler<CreateManualParkingLogCommand, Result<CreateParkingLogResponse>>
{
    private readonly IParkingLogRepository _parkingLogRepository;
    private readonly IVehicleRepository _vehicleRepository;
    private readonly IUserProfileRepository _userProfileRepository;
    private readonly IUserAccountRepository _userAccountRepository;
    private readonly IGuardRepository _guardRepository;
    private readonly IStudentRepository _studentRepository;
    private readonly IPersonnelRepository _personnelRepository;
    private readonly IAdminRepository _adminRepository;
    private readonly IViolationRepository _violationRepository;
    private readonly IParkingService _parkingService;
    private readonly IScheduleService _scheduleService;
    private readonly IParkingLogRoleService _parkingLogRoleService;

    public CreateManualParkingLogHandler(
        IParkingLogRepository parkingLogRepository,
        IVehicleRepository vehicleRepository,
        IUserProfileRepository userProfileRepository,
        IUserAccountRepository userAccountRepository,
        IGuardRepository guardRepository,
        IStudentRepository studentRepository,
        IPersonnelRepository personnelRepository,
        IAdminRepository adminRepository,
        IViolationRepository violationRepository,
        IParkingService parkingService,
        IScheduleService scheduleService,
        IParkingLogRoleService parkingLogRoleService)
    {
        _parkingLogRepository = parkingLogRepository;
        _vehicleRepository = vehicleRepository;
        _userProfileRepository = userProfileRepository;
        _userAccountRepository = userAccountRepository;
        _guardRepository = guardRepository;
        _studentRepository = studentRepository;
        _personnelRepository = personnelRepository;
        _adminRepository = adminRepository;
        _violationRepository = violationRepository;
        _parkingService = parkingService;
        _scheduleService = scheduleService;
        _parkingLogRoleService = parkingLogRoleService;
    }

    public async Task<Result<CreateParkingLogResponse>> Handle(CreateManualParkingLogCommand request, CancellationToken cancellationToken)
    {
        // 1. Find vehicle by plate number
        var vehicle = await _vehicleRepository.GetByPlateNumberAsync(request.PlateNumber);

        if (vehicle == null)
        {
            UserAccount? ownerAccount = null;

            // If phone number is provided, try to find the real user
            if (!string.IsNullOrWhiteSpace(request.PhoneNumber))
            {
                ownerAccount = await _userAccountRepository.GetByPhoneNumberAsync(request.PhoneNumber);
            }

            // If still no owner, look up or create the Guest user
            if (ownerAccount == null)
            {
                ownerAccount = await _userAccountRepository.GetByPhoneNumberAsync("+00000000000");
                if (ownerAccount == null)
                {
                    // Create guest account
                    ownerAccount = new UserAccount(string.Empty, "+00000000000");
                    ownerAccount.Verify(); // Verify guest status
                    await _userAccountRepository.AddAsync(ownerAccount);

                    var guestProfile = new UserProfile(ownerAccount.Id, "Guest", "User", null, null);
                    await _userProfileRepository.AddAsync(guestProfile);
                }
            }

            // Create and register new vehicle on the fly
            var qrPayload = $"{ownerAccount.Id}:{request.PlateNumber}:{request.Brand ?? "Unknown"}";
            var qrCodeHash = Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(qrPayload));

            vehicle = new Vehicle(
                ownerAccount.Id,
                request.PlateNumber,
                request.Brand ?? "Unknown",
                qrCodeHash,
                request.VehicleType
            );

            await _vehicleRepository.AddAsync(vehicle);
        }

        // 2. Fetch owner profile
        var ownerProfile = await _userProfileRepository.GetByUserIdAsync(vehicle.OwnerId);
        if (ownerProfile == null)
            return Result<CreateParkingLogResponse>.Failure("Owner profile not found.", ErrorCode.NotFound);

        // 3. Check duplicate active parking session
        var activeParkingLog = await _parkingLogRepository.GetActiveParkingLogByVehicleIdAsync(vehicle.Id);
        if (activeParkingLog != null)
        {
            var conflictResponse = new CreateParkingLogResponse
            {
                FirstName = ownerProfile.FirstName,
                LastName = ownerProfile.LastName,
                MiddleName = ownerProfile.MiddleName,
                PlateNumber = vehicle.PlateNumber,
                Brand = vehicle.Brand,
                QrCodeHash = vehicle.QrCodeHash,
                VehicleType = vehicle.VehicleType.ToString(),
                EntryMethod = activeParkingLog.EntryMethod.ToString()
            };

            return Result<CreateParkingLogResponse>.Failure(
                conflictResponse,
                "Vehicle is already parked.",
                ErrorCode.Conflict);
        }

        // 4. Check guard exists
        var userProfile = await _userProfileRepository.GetByUserIdAsync(request.UserId);
        if (userProfile == null)
            return Result<CreateParkingLogResponse>.Failure("User profile not found.", ErrorCode.NotFound);

        var guard = await _guardRepository.GetByUserProfileIdAsync(userProfile.Id);
        if (guard == null)
            return Result<CreateParkingLogResponse>.Failure("Guard not found.", ErrorCode.NotFound);

        // 5. Create Entry with manual method
        var parkingLog = _parkingService.CreateEntry(vehicle.Id, guard.UserProfileId, EntryMethod.Manual);
        await _parkingLogRepository.AddParkingLogAsync(parkingLog);

        // 6. Calculate maximum exit time if schedule exists (always null for manual/ignore all)
        DateTime? maximumExitTimeUtc = null;

        var student = await _studentRepository.GetByUserProfileIdAsync(ownerProfile.Id);
        var personnel = await _personnelRepository.GetByUserProfileIdAsync(ownerProfile.Id);
        var admin = await _adminRepository.GetByUserProfileIdAsync(ownerProfile.Id);

        var roleDetails = _parkingLogRoleService.GetRoleDetails(ownerProfile, student, personnel, admin);

        var response = new CreateParkingLogResponse
        {
            FirstName = ownerProfile.FirstName,
            LastName = ownerProfile.LastName,
            MiddleName = ownerProfile.MiddleName,
            Role = roleDetails.Role,
            Status = parkingLog.Status.ToString(),
            PhoneNumber = ownerProfile.UserAccount?.PhoneNumber ?? string.Empty,
            PlateNumber = vehicle.PlateNumber,
            Brand = vehicle.Brand,
            QrCodeHash = vehicle.QrCodeHash,
            VehicleType = vehicle.VehicleType.ToString(),
            EntryTime = parkingLog.EntryTime,
            EntryDate = parkingLog.EntryTime.Date,
            MaximumExitTime = maximumExitTimeUtc,
            EntryMethod = parkingLog.EntryMethod.ToString()
        };

        return Result<CreateParkingLogResponse>.Success(response, "Entry Confirmed");
    }
}
