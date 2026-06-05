using MediatR;
using ParkFlow.Application.Common;
using ParkFlow.Application.Features.Profiles.DTOs;
using ParkFlow.Application.Interfaces;

namespace ParkFlow.Application.Features.Profiles.Queries;

public class GetUserProfileByUserIdHandler : IRequestHandler<GetUserProfileByUserIdQuery, Result<UserProfileDto>>
{
    private readonly IUserProfileRepository _userProfileRepository;

    public GetUserProfileByUserIdHandler(IUserProfileRepository userProfileRepository)
    {
        _userProfileRepository = userProfileRepository;
    }

    public async Task<Result<UserProfileDto>> Handle(GetUserProfileByUserIdQuery request, CancellationToken cancellationToken)
    {
        if (request.UserId == Guid.Empty)
            return Result<UserProfileDto>.Failure("UserId is required.", ErrorCode.BadRequest);

        var profile = await _userProfileRepository.GetByUserIdAsync(request.UserId);

        if (profile == null)
            return Result<UserProfileDto>.Failure("User profile not found.", ErrorCode.NotFound);

        var dto = new UserProfileDto(
            profile.Id,
            profile.UserAccountId,
            profile.UserAccount.PhoneNumber ?? string.Empty,
            profile.FirstName,
            profile.LastName,
            profile.ProfilePictureUrl,
            profile.UserAccount.OnboardingStep,
            StudentNumber: profile.Student?.StudentNumber,
            EmployeeIdNumber: profile.Personnel?.IdCardNumber,
            Course: profile.Student?.Course,
            YearLevel: profile.Student?.YearLevel,
            Section: profile.Student?.Section,
            Department: profile.Personnel?.Department
            );

        return Result<UserProfileDto>.Success(dto, "User profile retrieved.");
    }
}
