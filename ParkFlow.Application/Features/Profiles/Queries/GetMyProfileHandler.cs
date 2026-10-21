using MediatR;
using ParkFlow.Application.Common;
using ParkFlow.Application.Features.Profiles.DTOs;
using ParkFlow.Application.Interfaces;
using ParkFlow.Domain.Enums;

namespace ParkFlow.Application.Features.Profiles.Queries;

public class GetMyProfileHandler
    : IRequestHandler<GetMyProfileQuery, Result<UserProfileDto>>
{
    private readonly IUserProfileRepository _userProfileRepository;
    private readonly IUserContext _userContext;
    private readonly ICorSubmissionRepository _corSubmissionRepository;
    private readonly IVehicleRepository? _vehicleRepository;
    private readonly ICacheService? _cacheService;
    private readonly IUserAccountRepository? _userAccountRepository;

    public GetMyProfileHandler(
        IUserProfileRepository userProfileRepository,
        IUserContext userContext,
        ICorSubmissionRepository corSubmissionRepository,
        IVehicleRepository? vehicleRepository = null,
        ICacheService? cacheService = null,
        IUserAccountRepository? userAccountRepository = null)
    {
        _userProfileRepository = userProfileRepository;
        _userContext = userContext;
        _corSubmissionRepository = corSubmissionRepository;
        _vehicleRepository = vehicleRepository;
        _cacheService = cacheService;
        _userAccountRepository = userAccountRepository;
    }

    public async Task<Result<UserProfileDto>> Handle(
        GetMyProfileQuery request,
        CancellationToken cancellationToken)
    {
        var userId = _userContext.GetUserId();

        // A deleted account's JWT may still be cryptographically valid. Do not return
        // its cached profile or mistake it for an account that has not created a profile yet.
        if (userId == Guid.Empty || _userAccountRepository != null && !await _userAccountRepository.ExistsAsync(userId))
            return Result<UserProfileDto>.Failure("This account no longer exists. Sign in or create a new account.", ErrorCode.Unauthorized);

        var cacheKey = CacheKeys.UserProfile(userId);

        if (_cacheService != null && userId != Guid.Empty)
        {
            var cached = await _cacheService.GetAsync<UserProfileDto>(cacheKey, cancellationToken);
            if (cached != null)
            {
                return Result<UserProfileDto>.Success(await ApplyVehicleApprovalAsync(cached), "User profile retrieved.");
            }
        }

        var profile = await _userProfileRepository.GetByUserIdAsync(userId);

        if (profile == null)
            return Result<UserProfileDto>.Failure("User profile not found.", ErrorCode.NotFound);

        if (profile.UserAccount != null && profile.UserAccount.Status == AccountStatus.Suspended)
        {
            return Result<UserProfileDto>.Failure("Your account has been suspended. Please contact the administrator.", ErrorCode.Forbidden);
        }

        var latestCor = await _corSubmissionRepository.GetLatestByUserIdAsync(profile.UserAccountId);
        var corStatus = latestCor?.VerificationStatus ?? (profile.Admin != null || profile.Guard != null
            ? CorVerificationStatus.Verified
            : CorVerificationStatus.NotSubmitted);

        string? rejectionReason = null;
        string? rejectedTarget = null;

        if (latestCor != null && latestCor.VerificationStatus == CorVerificationStatus.Rejected)
        {
            rejectionReason = latestCor.RejectionReason;
            rejectedTarget = "documents";
            corStatus = CorVerificationStatus.Rejected;
        }

        string? roleStr = null;
        if (profile.Admin != null)
        {
            roleStr = profile.Admin.RoleLevel == RoleLevel.SuperAdmin ? "SuperAdmin" : "Admin";
        }
        else if (profile.Guard != null)
        {
            roleStr = "Guard";
        }
        else if (profile.Student != null)
        {
            roleStr = "Student";
        }
        else if (profile.Personnel != null)
        {
            roleStr = profile.Personnel.Role == Roles.NonAcademicPersonnel
                ? "NonAcademicPersonnel"
                : "UniversityStaff";
        }

        var dto = new UserProfileDto(
            profile.Id,
            profile.UserAccountId,
            profile.UserAccount?.PhoneNumber ?? string.Empty,
            profile.FirstName,
            profile.LastName,
            profile.MiddleName,
            profile.ProfilePictureUrl,
            profile.UserAccount?.OnboardingStep ?? OnboardingStep.Profile,
            StudentNumber: profile.Student?.StudentNumber,
            EmployeeIdNumber: profile.Personnel?.IdCardNumber,
            Course: profile.Student?.Course,
            YearLevel: profile.Student?.YearLevel,
            Section: profile.Student?.Section,
            Department: profile.Personnel?.Department,
            CorVerificationStatus: corStatus,
            RejectionReason: rejectionReason,
            RejectedTarget: rejectedTarget,
            Status: profile.UserAccount?.Status.ToString(),
            Role: roleStr,
            Email: profile.UserAccount?.PrimaryEmail,
            ScheduleVerificationStatus: latestCor?.ScheduleVerificationStatus,
            DriversLicenseUrl: profile.DriversLicenseUrl, LicenseStatus: profile.LicenseStatus, LicenseRejectionReason: profile.LicenseRejectionReason);
        dto = await ApplyVehicleApprovalAsync(dto);

        if (_cacheService != null && userId != Guid.Empty)
        {
            await _cacheService.SetAsync(cacheKey, dto, TimeSpan.FromMinutes(5), cancellationToken);
        }

        return Result<UserProfileDto>.Success(dto, "User profile retrieved.");
    }
    private async Task<UserProfileDto> ApplyVehicleApprovalAsync(UserProfileDto dto)
    {
        // Existing accounts created by the previous flow must not appear verified
        // merely because their old status was Active. This does not write on GET.
        if (dto.Role == "Student" && dto.Status == "Active" && (dto.LicenseStatus != CorVerificationStatus.Verified || string.IsNullOrWhiteSpace(dto.ProfilePictureUrl)))
            return dto with { Status = AccountStatus.PendingVerification.ToString() };
        if (dto.Role == "Student" && dto.Status == "Active" && _vehicleRepository != null &&
            !(await _vehicleRepository.GetByOwnerIdAsync(dto.UserAccountId)).Any(v => v.VerificationStatus == CorVerificationStatus.Verified &&
                !string.IsNullOrWhiteSpace(v.OrcrDocumentUrl) && !string.IsNullOrWhiteSpace(v.VehiclePictureUrl)))
            return dto with { Status = AccountStatus.PendingVerification.ToString() };
        return dto;
    }
}
