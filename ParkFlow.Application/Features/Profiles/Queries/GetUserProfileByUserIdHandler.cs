using MediatR;
using ParkFlow.Application.Common;
using ParkFlow.Application.Features.Profiles.DTOs;
using ParkFlow.Application.Interfaces;
using ParkFlow.Domain.Enums;

namespace ParkFlow.Application.Features.Profiles.Queries;

public class GetUserProfileByUserIdHandler : IRequestHandler<GetUserProfileByUserIdQuery, Result<UserProfileDto>>
{
    private readonly IUserProfileRepository _userProfileRepository;
    private readonly ICorSubmissionRepository _corSubmissionRepository;
    private readonly IVehicleRepository? _vehicleRepository;

    public GetUserProfileByUserIdHandler(
        IUserProfileRepository userProfileRepository,
        ICorSubmissionRepository corSubmissionRepository,
        IVehicleRepository? vehicleRepository = null)
    {
        _userProfileRepository = userProfileRepository;
        _corSubmissionRepository = corSubmissionRepository;
        _vehicleRepository = vehicleRepository;
    }

    public async Task<Result<UserProfileDto>> Handle(GetUserProfileByUserIdQuery request, CancellationToken cancellationToken)
    {
        if (request.UserId == Guid.Empty)
            return Result<UserProfileDto>.Failure("UserId is required.", ErrorCode.BadRequest);

        var profile = await _userProfileRepository.GetByUserIdAsync(request.UserId);

        if (profile == null)
            return Result<UserProfileDto>.Failure("User profile not found.", ErrorCode.NotFound);

        var latestCor = await _corSubmissionRepository.GetLatestByUserIdAsync(profile.UserAccountId);
        var corStatus = latestCor?.VerificationStatus ?? CorVerificationStatus.NotSubmitted;

        string? rejectionReason = null;
        string? rejectedTarget = null;

        if (latestCor != null && latestCor.VerificationStatus == CorVerificationStatus.Rejected)
        {
            rejectionReason = latestCor.RejectionReason;
            rejectedTarget = "documents";
        }
        else if (_vehicleRepository != null)
        {
            var vehicles = await _vehicleRepository.GetByOwnerIdAsync(profile.UserAccountId);
            var rejectedVehicle = vehicles.FirstOrDefault(v => v.VerificationStatus == CorVerificationStatus.Rejected);
            if (rejectedVehicle != null)
            {
                corStatus = CorVerificationStatus.Rejected;
                rejectionReason = rejectedVehicle.RejectionReason;
                rejectedTarget = "vehicle";
            }
        }

        var dto = new UserProfileDto(
            profile.Id,
            profile.UserAccountId,
            profile.UserAccount.PhoneNumber ?? string.Empty,
            profile.FirstName,
            profile.LastName,
            profile.MiddleName,
            profile.ProfilePictureUrl,
            profile.UserAccount.OnboardingStep,
            StudentNumber: profile.Student?.StudentNumber,
            EmployeeIdNumber: profile.Personnel?.IdCardNumber,
            Course: profile.Student?.Course,
            YearLevel: profile.Student?.YearLevel,
            Section: profile.Student?.Section,
            Department: profile.Personnel?.Department,
            CorVerificationStatus: corStatus,
            RejectionReason: rejectionReason,
            RejectedTarget: rejectedTarget
            );

        return Result<UserProfileDto>.Success(dto, "User profile retrieved.");
    }
}
