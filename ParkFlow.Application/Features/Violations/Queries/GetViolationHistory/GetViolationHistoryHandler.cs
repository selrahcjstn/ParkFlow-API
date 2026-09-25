using MediatR;
using ParkFlow.Application.Common;
using ParkFlow.Application.Features.ParkingLogs.Services;
using ParkFlow.Application.Features.Violations.DTOs;
using ParkFlow.Application.Interfaces;

namespace ParkFlow.Application.Features.Violations.Queries.GetViolationHistory;

public class GetViolationHistoryHandler
    : IRequestHandler<GetViolationHistoryQuery, Result<PagedViolationHistoryResponse>>
{
    private readonly IViolationRepository _violationRepository;
    private readonly IUserProfileRepository _userProfileRepository;
    private readonly IGuardRepository _guardRepository;
    private readonly IAdminRepository _adminRepository;
    private readonly IParkingLogRoleService _parkingLogRoleService;

    public GetViolationHistoryHandler(
        IViolationRepository violationRepository,
        IUserProfileRepository userProfileRepository,
        IGuardRepository guardRepository,
        IAdminRepository adminRepository,
        IParkingLogRoleService parkingLogRoleService)
    {
        _violationRepository = violationRepository;
        _userProfileRepository = userProfileRepository;
        _guardRepository = guardRepository;
        _adminRepository = adminRepository;
        _parkingLogRoleService = parkingLogRoleService;
    }

    public async Task<Result<PagedViolationHistoryResponse>> Handle(
        GetViolationHistoryQuery request,
        CancellationToken cancellationToken)
    {
        // Determine if caller is a guard or admin — guards and admins see all violations
        var profile = await _userProfileRepository.GetByUserIdAsync(request.UserId);
        if (profile is null)
            return Result<PagedViolationHistoryResponse>.Failure("User profile not found.", ErrorCode.NotFound);

        var guard = await _guardRepository.GetByUserProfileIdAsync(profile.Id);
        var callerAdmin = await _adminRepository.GetByUserProfileIdAsync(profile.Id);
        var canViewAll = guard is not null || callerAdmin is not null;

        var violations = await _violationRepository.GetViolationHistoryAsync(
            canViewAll ? null : request.UserId,
            request.PageNumber,
            request.PageSize,
            request.UnpaidOnly);

        var items = new List<ViolationHistoryResponse>();

        foreach (var violation in violations)
        {
            var log = violation.ParkingLog;
            var vehicle = log?.Vehicle;
            var ownerProfile = vehicle?.Owner?.UserProfile;
            if (ownerProfile == null && vehicle?.OwnerId != null && vehicle.OwnerId != Guid.Empty)
            {
                ownerProfile = await _userProfileRepository.GetByUserIdAsync(vehicle.OwnerId);
            }

            var admin = ownerProfile != null ? await _adminRepository.GetByUserProfileIdAsync(ownerProfile.Id) : null;
            var roleDetails = ownerProfile != null
                ? _parkingLogRoleService.GetRoleDetails(
                    ownerProfile,
                    ownerProfile.Student,
                    ownerProfile.Personnel,
                    admin)
                : null;

            items.Add(new ViolationHistoryResponse
            {
                // Owner
                FirstName = ownerProfile?.FirstName ?? "Registered",
                LastName = ownerProfile?.LastName ?? "User",
                MiddleName = ownerProfile?.MiddleName,
                RoleName = roleDetails?.Role ?? "User",

                // Vehicle
                PlateNumber = vehicle?.PlateNumber ?? "N/A",
                Brand = vehicle?.Brand ?? "N/A",
                VehicleType = vehicle?.VehicleType.ToString() ?? "N/A",

                // Violation
                ViolationId = violation.Id,
                ReferenceNumber = violation.ReferenceNumber,
                ViolationType = violation.ViolationType.ToString(),
                PenaltyFee = violation.PenaltyFee,
                SettlementStatus = violation.SettlementStatus.ToString(),
                IsPaid = violation.SettlementStatus == global::SettlementStatus.Settled,

                // Session
                EntryTime = log?.EntryTime ?? violation.CreatedAt,
                ExitTime = log?.ExitTime ?? DateTime.UtcNow,
                IssuedAt = violation.CreatedAt
            });
        }

        var response = new PagedViolationHistoryResponse
        {
            GeneratedAt = DateTime.UtcNow,
            Items = items
        };

        return Result<PagedViolationHistoryResponse>.Success(response, "Violation history retrieved successfully.");
    }
}
